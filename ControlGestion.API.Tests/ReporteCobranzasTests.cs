using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using ControlGestion.API.Data;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>El reporte de cobranzas (3.3, 7.3.12), del gerente general, con los datos del 8.6.4.</summary>
[Collection(ColeccionApi.Nombre)]
public class ReporteCobranzasTests(ApiDePrueba api)
{
    /// <summary>
    /// Las cobranzas del 8.6.4 de un promotor propio (el producto vale Bs 1, así la cantidad es el monto), más una
    /// incobrable de 50 y una anulada de 30.
    /// </summary>
    private sealed record Escenario(string PromotorId, int Vencida, int Incobrable, int ClienteDeLaVencida);

    private async Task<Escenario> CobranzasDelDisenoAsync()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var vendedor = api.ClienteCon(promotor.Token);
        var zonaId = await operador.CrearZonaAsync(promotor.Id);
        var productoId = await operador.CrearProductoAsync(1m, stockInicial: 2000);

        async Task<int> CobranzaAsync(decimal monto, decimal pagado = 0)
        {
            var id = await vendedor.CrearCobranzaAsync(await operador.CrearClienteAsync(zonaId), productoId, monto);
            if (pagado > 0)
                (await operador.PostAsJsonAsync($"/api/cobranzas/{id}/pagos", new { monto = pagado, metodoPago = 0 }))
                    .EnsureSuccessStatusCode();
            return id;
        }

        await CobranzaAsync(76);
        await CobranzaAsync(152);
        await CobranzaAsync(175, 50);
        await CobranzaAsync(240, 100);
        await CobranzaAsync(99, 99);
        var vencida = await CobranzaAsync(327);
        var incobrable = await CobranzaAsync(50);
        (await operador.PostAsJsonAsync($"/api/cobranzas/{incobrable}/incobrable", new { observacion = "Cerró" })).EnsureSuccessStatusCode();
        var anulada = await CobranzaAsync(30);
        var hojaId = (await (await operador.GetAsync($"/api/cobranzas/{anulada}")).LeerJsonAsync())
            .GetProperty("hojaPedido").GetProperty("id").GetInt32();
        (await operador.PostAsJsonAsync($"/api/hojas-pedido/{hojaId}/anular", new { motivo = "Venta duplicada" })).EnsureSuccessStatusCode();

        // La de Marta: venció hace 14 días.
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var vencimiento = DateTime.UtcNow.AddDays(-14);
        await db.Cobranzas.Where(c => c.Id == vencida).ExecuteUpdateAsync(s => s
            .SetProperty(c => c.Estado, EstadoCobranza.Vencida)
            .SetProperty(c => c.FechaVencimiento, vencimiento));
        var clienteDeLaVencida = await db.Cobranzas.Where(c => c.Id == vencida).Select(c => c.ClienteId).SingleAsync();
        return new Escenario(promotor.Id, vencida, incobrable, clienteDeLaVencida);
    }

    private async Task<HttpClient> GerenteAsync() => api.ClienteCon((await api.CrearUsuarioAsync(Roles.GerenteGeneral)).Token);

    private static (string?, int, decimal, decimal, decimal) Fila(JsonElement e) => (e.GetProperty("nombre").GetString(),
        e.GetProperty("cantidad").GetInt32(), e.GetProperty("emitido").GetDecimal(), e.GetProperty("cobrado").GetDecimal(),
        e.GetProperty("saldo").GetDecimal());

    [Fact]
    public async Task Los_totales_por_estado_son_los_del_8_6_4_y_el_total_no_suma_las_incobrables_ni_las_anuladas()
    {
        var escenario = await CobranzasDelDisenoAsync();
        using var gerente = await GerenteAsync();

        var reporte = await (await gerente.GetAsync($"/api/cobranzas/reporte?promotorId={escenario.PromotorId}")).LeerJsonAsync();

        Assert.Equal(
            [("Pendiente", 2, 228m, 0m, 228m), ("Parcial", 2, 415m, 150m, 265m), ("Vencida", 1, 327m, 0m, 327m),
             ("Pagada", 1, 99m, 99m, 0m), ("Incobrable", 1, 50m, 0m, 50m), ("Anulada", 1, 30m, 0m, 30m)],
            reporte.GetProperty("porEstado").EnumerateArray().Select(Fila).ToList());
        var total = reporte.GetProperty("total");
        Assert.Equal((6, 1069m, 249m, 820m), (total.GetProperty("cantidad").GetInt32(), total.GetProperty("emitido").GetDecimal(),
            total.GetProperty("cobrado").GetDecimal(), total.GetProperty("saldo").GetDecimal()));
        Assert.Equal(8, reporte.GetProperty("cobranzas").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Van_de_la_que_vence_primero_y_la_vencida_trae_sus_dias_de_mora()
    {
        var escenario = await CobranzasDelDisenoAsync();
        using var gerente = await GerenteAsync();

        var pagina = (await (await gerente.GetAsync($"/api/cobranzas/reporte?promotorId={escenario.PromotorId}&tamano=2"))
            .LeerJsonAsync()).GetProperty("cobranzas");

        var primera = pagina.GetProperty("items")[0];
        Assert.Equal(escenario.Vencida, primera.GetProperty("id").GetInt32());
        Assert.Equal(14, primera.GetProperty("diasDeMora").GetInt32());
        Assert.Equal(JsonValueKind.Null, pagina.GetProperty("items")[1].GetProperty("diasDeMora").ValueKind);
        Assert.Equal(4, pagina.GetProperty("totalPaginas").GetInt32());
    }

    [Fact]
    public async Task Filtra_por_estado_cliente_y_dias_de_emision()
    {
        var escenario = await CobranzasDelDisenoAsync();
        using var gerente = await GerenteAsync();
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Emitida el 15/09/2026 a las 21:30 de Bolivia: en UTC ya es el 16.
            var emision = new DateTime(2026, 9, 16, 1, 30, 0, DateTimeKind.Utc);
            await db.Cobranzas.Where(c => c.Id == escenario.Vencida).ExecuteUpdateAsync(s => s.SetProperty(c => c.FechaEmision, emision));
        }
        async Task<List<int>> IdsAsync(string filtros) =>
            (await (await gerente.GetAsync($"/api/cobranzas/reporte?promotorId={escenario.PromotorId}&{filtros}")).LeerJsonAsync())
            .GetProperty("cobranzas").GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).ToList();

        Assert.Equal(2, (await IdsAsync("estado=1")).Count);
        Assert.Equal([escenario.Incobrable], await IdsAsync("estado=4"));
        Assert.Equal([escenario.Vencida], await IdsAsync($"clienteId={escenario.ClienteDeLaVencida}"));
        Assert.Equal([escenario.Vencida], await IdsAsync("desde=2026-09-15&hasta=2026-09-15"));
        Assert.Empty(await IdsAsync("desde=2026-09-16&hasta=2026-09-16"));
    }

    [Fact]
    public async Task Sin_cartera_completa_solo_cuenta_las_cobranzas_de_su_cartera()
    {
        var escenario = await CobranzasDelDisenoAsync();
        using var admin = await api.AdminAsync();
        var (_, rol) = await admin.CrearRolAsync(Permisos.Cobranzas.GenerarReporte, Permisos.Territorial.RecorrerRuta);
        var usuario = await api.CrearUsuarioAsync(rol);
        using var cliente = api.ClienteCon(usuario.Token);

        var reporte = await (await cliente.GetAsync($"/api/cobranzas/reporte?promotorId={escenario.PromotorId}")).LeerJsonAsync();

        Assert.Empty(reporte.GetProperty("porEstado").EnumerateArray());
        Assert.Equal(0, reporte.GetProperty("total").GetProperty("cantidad").GetInt32());
        Assert.Equal(0, reporte.GetProperty("cobranzas").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task El_Excel_trae_las_cobranzas_y_los_totales_con_la_fila_Total()
    {
        var escenario = await CobranzasDelDisenoAsync();
        using var gerente = await GerenteAsync();

        var respuesta = await gerente.GetAsync($"/api/cobranzas/reporte/excel?promotorId={escenario.PromotorId}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.StartsWith("cobranzas_", respuesta.Content.Headers.ContentDisposition?.FileNameStar ?? respuesta.Content.Headers.ContentDisposition?.FileName);
        using var libro = new XLWorkbook(await respuesta.Content.ReadAsStreamAsync());
        var cobranzas = libro.Worksheet("Cobranzas");
        Assert.Equal(9, cobranzas.LastRowUsed()!.RowNumber());
        Assert.Equal("Vencida", cobranzas.Cell(2, 10).GetString());
        Assert.Equal(14, cobranzas.Cell(2, 6).GetValue<int>());
        Assert.Equal(327m, cobranzas.Cell(2, 9).GetValue<decimal>());

        var totales = libro.Worksheet("Totales por estado");
        Assert.Equal("Total", totales.Cell(8, 1).GetString());
        Assert.Equal(1069m, totales.Cell(8, 3).GetValue<decimal>());
        Assert.Equal(820m, totales.Cell(8, 5).GetValue<decimal>());
        Assert.Equal("La fila Total no suma las cobranzas incobrables ni las anuladas.", totales.Cell(10, 1).GetString());
    }

    [Fact]
    public async Task Rechaza_un_rango_al_reves_sin_cobranzas_no_exporta_y_pide_su_permiso()
    {
        using var gerente = await GerenteAsync();
        await (await gerente.GetAsync("/api/cobranzas/reporte?desde=2026-10-21&hasta=2026-10-20")).ProblemaAsync(400, "validacion");
        await (await gerente.GetAsync("/api/cobranzas/reporte/por-cliente?desde=2026-10-21&hasta=2026-10-20")).ProblemaAsync(400, "validacion");
        await (await gerente.GetAsync("/api/cobranzas/reporte/excel?desde=2001-01-01&hasta=2001-01-01"))
            .ProblemaAsync(400, "reporte_sin_datos");

        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await secretaria.GetAsync("/api/cobranzas/reporte")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await secretaria.GetAsync("/api/cobranzas/reporte/por-cliente")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await secretaria.GetAsync("/api/cobranzas/reporte/excel")).StatusCode);
    }

    /// <summary>
    /// Tres clientes de un promotor propio (tarea 30): Rosa, con dos créditos (240 con 100 pagados y 109); Marta, con una
    /// vencida de 327 hace 14 días; y Jorge, con 175 y 50 pagados, más una incobrable de 50 y una anulada de 30.
    /// </summary>
    private sealed record Clientes(string PromotorId, int Rosa, int Marta, int Jorge, int[] DeRosa, int Vencida);

    private async Task<Clientes> TresClientesAsync()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var vendedor = api.ClienteCon(promotor.Token);
        var zonaId = await operador.CrearZonaAsync(promotor.Id);
        var productoId = await operador.CrearProductoAsync(1m, stockInicial: 2000);
        var (rosa, marta, jorge) = (await operador.CrearClienteAsync(zonaId), await operador.CrearClienteAsync(zonaId),
            await operador.CrearClienteAsync(zonaId));

        async Task<int> CobranzaAsync(int clienteId, decimal monto, decimal pagado = 0)
        {
            var id = await vendedor.CrearCobranzaAsync(clienteId, productoId, monto);
            if (pagado > 0)
                (await operador.PostAsJsonAsync($"/api/cobranzas/{id}/pagos", new { monto = pagado, metodoPago = 0 }))
                    .EnsureSuccessStatusCode();
            return id;
        }

        int[] deRosa = [await CobranzaAsync(rosa, 240, 100), await CobranzaAsync(rosa, 109)];
        var vencida = await CobranzaAsync(marta, 327);
        await CobranzaAsync(jorge, 175, 50);
        var incobrable = await CobranzaAsync(jorge, 50);
        (await operador.PostAsJsonAsync($"/api/cobranzas/{incobrable}/incobrable", new { observacion = "Cerró" })).EnsureSuccessStatusCode();
        var anulada = await CobranzaAsync(jorge, 30);
        var hojaId = (await (await operador.GetAsync($"/api/cobranzas/{anulada}")).LeerJsonAsync())
            .GetProperty("hojaPedido").GetProperty("id").GetInt32();
        (await operador.PostAsJsonAsync($"/api/hojas-pedido/{hojaId}/anular", new { motivo = "Venta duplicada" })).EnsureSuccessStatusCode();

        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var vencimiento = DateTime.UtcNow.AddDays(-14);
        await db.Cobranzas.Where(c => c.Id == vencida).ExecuteUpdateAsync(s => s
            .SetProperty(c => c.Estado, EstadoCobranza.Vencida)
            .SetProperty(c => c.FechaVencimiento, vencimiento));
        return new Clientes(promotor.Id, rosa, marta, jorge, deRosa, vencida);
    }

    private static (int, int, decimal, decimal, decimal, decimal, int?) Deuda(JsonElement c) => (
        c.GetProperty("cliente").GetProperty("id").GetInt32(), c.GetProperty("cantidad").GetInt32(),
        c.GetProperty("emitido").GetDecimal(), c.GetProperty("cobrado").GetDecimal(), c.GetProperty("saldo").GetDecimal(),
        c.GetProperty("vencido").GetDecimal(),
        c.GetProperty("diasDeMora").ValueKind == JsonValueKind.Null ? null : c.GetProperty("diasDeMora").GetInt32());

    [Fact]
    public async Task Por_cliente_hay_una_fila_por_cliente_con_sus_cobranzas_de_quien_mas_debe_a_quien_menos()
    {
        var escenario = await TresClientesAsync();
        using var gerente = await GerenteAsync();

        var reporte = await (await gerente.GetAsync($"/api/cobranzas/reporte/por-cliente?promotorId={escenario.PromotorId}"))
            .LeerJsonAsync();

        var clientes = reporte.GetProperty("clientes");
        Assert.Equal(3, clientes.GetProperty("total").GetInt32());
        var filas = clientes.GetProperty("items").EnumerateArray().ToList();
        // Jorge: la incobrable y la anulada no suman, como en la fila Total, pero están en su detalle.
        Assert.Equal([(escenario.Marta, 1, 327m, 0m, 327m, 327m, 14), (escenario.Rosa, 2, 349m, 100m, 249m, 0m, null),
            (escenario.Jorge, 3, 175m, 50m, 125m, 0m, null)], filas.Select(Deuda).ToList());
        Assert.All(filas, f => Assert.Equal(escenario.PromotorId, f.GetProperty("promotor").GetProperty("id").GetString()));
        var deRosa = filas[1].GetProperty("cobranzas").EnumerateArray().ToList();
        Assert.Equal(escenario.DeRosa, deRosa.Select(c => c.GetProperty("id").GetInt32()));
        Assert.Equal([140m, 109m], deRosa.Select(c => c.GetProperty("saldoPendiente").GetDecimal()));
        Assert.Equal(14, filas[0].GetProperty("cobranzas")[0].GetProperty("diasDeMora").GetInt32());
        Assert.Equal(["Parcial", "Incobrable", "Anulada"], filas[2].GetProperty("cobranzas").EnumerateArray()
            .Select(c => ((EstadoCobranza)c.GetProperty("estado").GetInt32()).ToString()));
        // Los totales por estado son los del reporte de siempre.
        var deSiempre = await (await gerente.GetAsync($"/api/cobranzas/reporte?promotorId={escenario.PromotorId}")).LeerJsonAsync();
        Assert.Equal(deSiempre.GetProperty("total").GetRawText(), reporte.GetProperty("total").GetRawText());
        Assert.Equal(deSiempre.GetProperty("porEstado").GetRawText(), reporte.GetProperty("porEstado").GetRawText());
    }

    [Fact]
    public async Task Por_cliente_filtra_pagina_y_respeta_la_cartera()
    {
        var escenario = await TresClientesAsync();
        using var gerente = await GerenteAsync();
        async Task<JsonElement> ClientesAsync(string filtros) =>
            (await (await gerente.GetAsync($"/api/cobranzas/reporte/por-cliente?promotorId={escenario.PromotorId}&{filtros}"))
                .LeerJsonAsync()).GetProperty("clientes");

        var vencidas = await ClientesAsync("estado=3");
        Assert.Equal(escenario.Marta, Assert.Single(vencidas.GetProperty("items").EnumerateArray()).GetProperty("cliente").GetProperty("id").GetInt32());
        var deRosa = Assert.Single((await ClientesAsync($"clienteId={escenario.Rosa}")).GetProperty("items").EnumerateArray());
        Assert.Equal(2, deRosa.GetProperty("cantidad").GetInt32());
        var pagina = await ClientesAsync("tamano=2&pagina=2");
        Assert.Equal((3, 2), (pagina.GetProperty("total").GetInt32(), pagina.GetProperty("totalPaginas").GetInt32()));
        Assert.Equal(escenario.Jorge, Assert.Single(pagina.GetProperty("items").EnumerateArray()).GetProperty("cliente").GetProperty("id").GetInt32());

        using var admin = await api.AdminAsync();
        var (_, rol) = await admin.CrearRolAsync(Permisos.Cobranzas.GenerarReporte, Permisos.Territorial.RecorrerRuta);
        using var otro = api.ClienteCon((await api.CrearUsuarioAsync(rol)).Token);
        var ajeno = await (await otro.GetAsync($"/api/cobranzas/reporte/por-cliente?promotorId={escenario.PromotorId}")).LeerJsonAsync();
        Assert.Equal(0, ajeno.GetProperty("clientes").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task El_Excel_suma_la_hoja_por_cliente()
    {
        var escenario = await TresClientesAsync();
        using var gerente = await GerenteAsync();

        var respuesta = await gerente.GetAsync($"/api/cobranzas/reporte/excel?promotorId={escenario.PromotorId}");

        using var libro = new XLWorkbook(await respuesta.Content.ReadAsStreamAsync());
        Assert.Equal(["Cobranzas", "Por cliente", "Totales por estado"], libro.Worksheets.Select(h => h.Name));
        var hoja = libro.Worksheet("Por cliente");
        Assert.Equal("Días de mora", hoja.Cell(1, 8).GetString());
        Assert.Equal([327m, 249m, 125m], [hoja.Cell(2, 6).GetValue<decimal>(), hoja.Cell(3, 6).GetValue<decimal>(), hoja.Cell(4, 6).GetValue<decimal>()]);
        Assert.Equal((2, 14), (hoja.Cell(3, 3).GetValue<int>(), hoja.Cell(2, 8).GetValue<int>()));
        Assert.Equal("Los montos de cada cliente no suman las cobranzas incobrables ni las anuladas.", hoja.Cell(6, 1).GetString());
    }
}
