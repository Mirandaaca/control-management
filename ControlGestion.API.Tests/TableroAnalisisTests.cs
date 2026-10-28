using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>El análisis de Inicio (tarea 24, parte B): cada bloque con la cartera del usuario y el permiso de su reporte.</summary>
[Collection(ColeccionApi.Nombre)]
public class TableroAnalisisTests(ApiDePrueba api)
{
    private const string Ruta = "/api/reportes/tablero/analisis";

    [Fact]
    public async Task Con_los_permisos_de_los_reportes_ve_cada_bloque_con_su_cartera()
    {
        using var operador = await api.OperadorAsync();
        using var admin = await api.AdminAsync();
        // Sin cartera completa: los números son solo de sus clientes, y no se mezclan con los de las otras pruebas. Recorre
        // rutas para poder tener una zona.
        var (_, rol) = await admin.CrearRolAsync(Permisos.Reportes.VentasPorZona, Permisos.Cobranzas.ConsultarPagos,
            Permisos.Cobranzas.ConsultarDeudas, Permisos.Ventas.RegistrarCliente, Permisos.Territorial.PlanificarRuta,
            Permisos.Territorial.RecorrerRuta);
        var usuario = await api.CrearUsuarioAsync(rol);
        using var cliente = api.ClienteCon(usuario.Token);
        var zonaId = await operador.CrearZonaAsync(usuario.Id);
        var ana = await operador.CrearClienteAsync(zonaId);
        var beto = await operador.CrearClienteAsync(zonaId);
        var productoId = await operador.CrearProductoAsync(1m, stockInicial: 100);
        async Task<int> ConfirmadaAsync(int clienteId, decimal monto, bool credito)
        {
            var id = await operador.CrearHojaPedidoAsync(clienteId, productoId, monto, credito);
            (await operador.PostAsync($"/api/hojas-pedido/{id}/confirmar", null)).EnsureSuccessStatusCode();
            return id;
        }
        await ConfirmadaAsync(ana, 10, credito: false);
        var deBeto = await ConfirmadaAsync(beto, 20, credito: true);
        var delMesAnterior = await ConfirmadaAsync(ana, 7, credito: true);
        async Task<int> CobranzaDeAsync(int hojaId)
        {
            using var scope = api.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Cobranzas
                .Where(c => c.HojaPedidoId == hojaId).Select(c => c.Id).SingleAsync();
        }
        async Task<int> PagarAsync(int cobranzaId, decimal monto)
        {
            var respuesta = await operador.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos", new { monto, metodoPago = 0 });
            respuesta.EnsureSuccessStatusCode();
            return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt32();
        }
        var cobranzaDeBeto = await CobranzaDeAsync(deBeto);
        var cobranzaAnterior = await CobranzaDeAsync(delMesAnterior);
        await PagarAsync(cobranzaDeBeto, 5);
        var pagoAnterior = await PagarAsync(cobranzaAnterior, 2);
        var hoy = HoraBolivia.Hoy(TimeProvider.System);
        (await operador.PostAsJsonAsync("/api/rutas", new { zonaId, promotorId = usuario.Id, fecha = $"{hoy:yyyy-MM-dd}", clientes = new[] { ana, beto } }))
            .EnsureSuccessStatusCode();

        // Lo del mes anterior, el día 1 al mediodía de Bolivia: dentro del tramo que se compara, sea cual sea el día de hoy.
        var primeroAnterior = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-1);
        var mediodiaAnterior = primeroAnterior.ToDateTime(new TimeOnly(16, 0), DateTimeKind.Utc);
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.HojasPedido.Where(h => h.Id == delMesAnterior).ExecuteUpdateAsync(s => s.SetProperty(h => h.FechaRegistro, mediodiaAnterior));
            await db.PagosCobranza.Where(p => p.Id == pagoAnterior).ExecuteUpdateAsync(s => s.SetProperty(p => p.FechaPago, mediodiaAnterior));
            await db.Clientes.Where(c => c.Id == beto).ExecuteUpdateAsync(s => s.SetProperty(c => c.FechaCreacion, mediodiaAnterior));
            // La deuda del mes anterior venció hace 45 días: va en el tramo de 31 a 60.
            await db.Cobranzas.Where(c => c.Id == cobranzaAnterior).ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Estado, EstadoCobranza.Vencida)
                .SetProperty(c => c.FechaVencimiento, HoraBolivia.InicioDelDiaUtc(hoy.AddDays(-45)).AddHours(12)));
            await db.RutasDetalle.Where(d => d.ClienteId == ana).ExecuteUpdateAsync(s => s.SetProperty(d => d.EstadoVisita, EstadoVisita.Visitado));
            await db.RutasDetalle.Where(d => d.ClienteId == beto).ExecuteUpdateAsync(s => s.SetProperty(d => d.EstadoVisita, EstadoVisita.Ausente));
        }

        var respuesta = await cliente.GetAsync(Ruta);
        Assert.True(respuesta.IsSuccessStatusCode, await respuesta.Content.ReadAsStringAsync());
        var analisis = await respuesta.LeerJsonAsync();

        Assert.Equal($"{hoy:yyyy-MM-dd}", analisis.GetProperty("hoy").GetString());
        Assert.Equal((30m, 7m), Variacion(analisis, "ventas"));
        Assert.Equal((5m, 2m), Variacion(analisis, "cobrado"));
        Assert.Equal((1m, 1m), Variacion(analisis, "clientesNuevos"));

        var tendencia = analisis.GetProperty("tendencia").EnumerateArray().ToList();
        Assert.Equal(6, tendencia.Count);
        Assert.Equal($"{hoy:yyyy-MM}", tendencia[5].GetProperty("mes").GetString());
        Assert.Equal((10m, 20m), (tendencia[5].GetProperty("contado").GetDecimal(), tendencia[5].GetProperty("credito").GetDecimal()));
        Assert.Equal($"{primeroAnterior:yyyy-MM}", tendencia[4].GetProperty("mes").GetString());
        Assert.Equal((0m, 7m), (tendencia[4].GetProperty("contado").GetDecimal(), tendencia[4].GetProperty("credito").GetDecimal()));
        Assert.All(tendencia.Take(4), m => Assert.Equal(0m, m.GetProperty("contado").GetDecimal() + m.GetProperty("credito").GetDecimal()));

        var antiguedad = analisis.GetProperty("antiguedad").EnumerateArray()
            .ToDictionary(t => t.GetProperty("tramo").GetString()!, t => (t.GetProperty("cantidad").GetInt32(), t.GetProperty("saldo").GetDecimal()));
        Assert.Equal(["al-dia", "1-30", "31-60", "61-90", "mas-de-90"], antiguedad.Keys);
        Assert.Equal((1, 15m), antiguedad["al-dia"]);
        Assert.Equal((1, 5m), antiguedad["31-60"]);
        Assert.Equal((0, 0m), antiguedad["1-30"]);

        // Este mes: Beto compró 20 y Ana 10 (lo de Ana del mes anterior no cuenta).
        Assert.Equal([(beto, 20m), (ana, 10m)], analisis.GetProperty("mejoresClientes").EnumerateArray()
            .Select(c => (c.GetProperty("clienteId").GetInt32(), c.GetProperty("total").GetDecimal())));

        var visitas = Assert.Single(analisis.GetProperty("visitas").EnumerateArray());
        Assert.Equal(usuario.Id, visitas.GetProperty("promotorId").GetString());
        Assert.Equal((2, 1, 1), (visitas.GetProperty("planificadas").GetInt32(), visitas.GetProperty("visitadas").GetInt32(),
            visitas.GetProperty("ausentes").GetInt32()));
    }

    [Fact]
    public async Task Cada_bloque_solo_con_el_permiso_de_su_reporte()
    {
        using var admin = await api.AdminAsync();
        var (_, rol) = await admin.CrearRolAsync(Permisos.Cobranzas.ConsultarDeudas);
        using var deudas = api.ClienteCon((await api.CrearUsuarioAsync(rol)).Token);

        var analisis = await (await deudas.GetAsync(Ruta)).LeerJsonAsync();

        Assert.Equal(JsonValueKind.Array, analisis.GetProperty("antiguedad").ValueKind);
        foreach (var bloque in new[] { "ventas", "cobrado", "clientesNuevos", "tendencia", "mejoresClientes", "visitas" })
            Assert.Equal(JsonValueKind.Null, analisis.GetProperty(bloque).ValueKind);

        // El administrador del sistema no tiene permisos del negocio: ningún bloque.
        var delAdmin = await (await admin.GetAsync(Ruta)).LeerJsonAsync();
        foreach (var bloque in new[] { "ventas", "cobrado", "clientesNuevos", "tendencia", "antiguedad", "mejoresClientes", "visitas" })
            Assert.Equal(JsonValueKind.Null, delAdmin.GetProperty(bloque).ValueKind);
    }

    private static (decimal Actual, decimal Anterior) Variacion(JsonElement analisis, string bloque) =>
        (analisis.GetProperty(bloque).GetProperty("actual").GetDecimal(), analisis.GetProperty(bloque).GetProperty("anterior").GetDecimal());
}
