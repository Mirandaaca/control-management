using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>El tablero de Inicio: el mes en curso en Bolivia y la cartera del usuario.</summary>
[Collection(ColeccionApi.Nombre)]
public class TableroTests(ApiDePrueba api)
{
    [Fact]
    public async Task Un_promotor_ve_solo_lo_suyo_y_el_mes_empieza_a_las_00_00_de_Bolivia()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var vendedor = api.ClienteCon(promotor.Token);
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id));
        var productoId = await operador.CrearProductoAsync(1m, stockInicial: 30);
        async Task<int> ConfirmadaAsync(decimal monto, bool credito)
        {
            var id = await vendedor.CrearHojaPedidoAsync(clienteId, productoId, monto, credito);
            (await vendedor.PostAsync($"/api/hojas-pedido/{id}/confirmar", null)).EnsureSuccessStatusCode();
            return id;
        }
        var delMes = await ConfirmadaAsync(10, credito: true);
        var delMesAnterior = await ConfirmadaAsync(7, credito: false);
        // Una venta de otro promotor, que no es de su cartera.
        await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(await operador.CrearZonaAsync()), productoId, 3);

        // El 1 del mes a las 00:30 de Bolivia es del mes; el último día del anterior a las 23:30 de Bolivia, no, aunque en
        // UTC ya sea el 1.
        var hoy = HoraBolivia.Hoy(TimeProvider.System);
        var primero = new DateOnly(hoy.Year, hoy.Month, 1);
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dentro = primero.ToDateTime(new TimeOnly(4, 30), DateTimeKind.Utc);
            var fuera = primero.ToDateTime(new TimeOnly(3, 30), DateTimeKind.Utc);
            await db.HojasPedido.Where(h => h.Id == delMes).ExecuteUpdateAsync(s => s.SetProperty(h => h.FechaRegistro, dentro));
            await db.HojasPedido.Where(h => h.Id == delMesAnterior).ExecuteUpdateAsync(s => s.SetProperty(h => h.FechaRegistro, fuera));
        }

        var tablero = await (await vendedor.GetAsync("/api/reportes/tablero")).LeerJsonAsync();

        Assert.Equal(1, tablero.GetProperty("clientesActivos").GetInt32());
        Assert.Equal(1, tablero.GetProperty("hojasDelMes").GetInt32());
        Assert.Equal(10m, tablero.GetProperty("ventasDelMes").GetDecimal());
        Assert.Equal(10m, tablero.GetProperty("porCobrar").GetDecimal());
        Assert.Equal([promotor.Id], tablero.GetProperty("porPromotor").EnumerateArray().Select(p => p.GetProperty("promotorId").GetString()));
        var dia = Assert.Single(tablero.GetProperty("porDia").EnumerateArray());
        Assert.Equal($"{primero:yyyy-MM-dd}", dia.GetProperty("fecha").GetString());
        Assert.Equal(10m, Assert.Single(tablero.GetProperty("porProducto").EnumerateArray()).GetProperty("total").GetDecimal());
    }
}
