using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using ControlGestion.API.Data;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>Las ventas por zona (6.2, 7.3.23) y por promotor (6.1, 7.3.22), del gerente general.</summary>
[Collection(ColeccionApi.Nombre)]
public class ReporteVentasTests(ApiDePrueba api)
{
    // Cada prueba mueve sus HP a un día propio de 2002, que ninguna otra usa: así el período solo trae las suyas (cada
    // corrida crea su base).

    // A las 12:00 de Bolivia (16:00 UTC).
    private async Task MoverAsync(DateOnly dia, params int[] hojas)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fecha = dia.ToDateTime(new TimeOnly(16, 0), DateTimeKind.Utc);
        await db.HojasPedido.Where(h => hojas.Contains(h.Id)).ExecuteUpdateAsync(s => s.SetProperty(h => h.FechaRegistro, fecha));
    }

    private static async Task<int> ConfirmadaAsync(HttpClient vendedor, int clienteId, int productoId, decimal cantidad, bool credito)
    {
        var id = await vendedor.CrearHojaPedidoAsync(clienteId, productoId, cantidad, credito);
        (await vendedor.PostAsync($"/api/hojas-pedido/{id}/confirmar", null)).EnsureSuccessStatusCode();
        return id;
    }

    private async Task<HttpClient> GerenteAsync() => api.ClienteCon((await api.CrearUsuarioAsync(Roles.GerenteGeneral)).Token);

    private static JsonElement ZonaDe(JsonElement reporte, int zonaId) =>
        reporte.GetProperty("zonas").EnumerateArray().Single(z => z.GetProperty("zonaId").GetInt32() == zonaId);

    private static (int, decimal, decimal, decimal) Cifras(JsonElement e) => (e.GetProperty("cantidad").GetInt32(),
        e.GetProperty("total").GetDecimal(), e.GetProperty("contado").GetDecimal(), e.GetProperty("credito").GetDecimal());

    [Fact]
    public async Task Cada_zona_trae_sus_HP_confirmadas_al_contado_y_a_credito_y_la_que_no_vendio_sale_en_cero()
    {
        using var operador = await api.OperadorAsync();
        var conVentas = await operador.CrearZonaAsync();
        var sinVentas = await operador.CrearZonaAsync();
        var clienteId = await operador.CrearClienteAsync(conVentas);
        var productoId = await operador.CrearProductoAsync(1m, stockInicial: 100);
        var contado = await ConfirmadaAsync(operador, clienteId, productoId, 10, credito: false);
        var credito = await ConfirmadaAsync(operador, clienteId, productoId, 20, credito: true);
        var borrador = await operador.CrearHojaPedidoAsync(clienteId, productoId, 5);
        var anulada = await ConfirmadaAsync(operador, clienteId, productoId, 7, credito: false);
        (await operador.PostAsJsonAsync($"/api/hojas-pedido/{anulada}/anular", new { motivo = "Venta duplicada" })).EnsureSuccessStatusCode();
        var dia = new DateOnly(2002, 1, 10);
        await MoverAsync(dia, contado, credito, borrador, anulada);
        using var gerente = await GerenteAsync();

        var reporte = await (await gerente.GetAsync($"/api/reportes/ventas-por-zona?desde={dia:yyyy-MM-dd}&hasta={dia:yyyy-MM-dd}"))
            .LeerJsonAsync();

        Assert.Equal((2, 30m, 10m, 20m), Cifras(ZonaDe(reporte, conVentas)));
        Assert.Equal((0, 0m, 0m, 0m), Cifras(ZonaDe(reporte, sinVentas)));
        Assert.Equal((2, 30m, 10m, 20m), Cifras(reporte.GetProperty("total")));
        // La que más vendió va primero.
        Assert.Equal(conVentas, reporte.GetProperty("zonas")[0].GetProperty("zonaId").GetInt32());
    }

    [Fact]
    public async Task Una_venta_de_las_21_30_de_Bolivia_cuenta_ese_dia_aunque_en_UTC_ya_sea_el_siguiente()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var hojaId = await ConfirmadaAsync(operador, await operador.CrearClienteAsync(zonaId),
            await operador.CrearProductoAsync(1m, stockInicial: 10), 4, credito: false);
        var dia = new DateOnly(2002, 2, 10);
        using (var scope = api.Services.CreateScope())
        {
            var noche = dia.AddDays(1).ToDateTime(new TimeOnly(1, 30), DateTimeKind.Utc);
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().HojasPedido.Where(h => h.Id == hojaId)
                .ExecuteUpdateAsync(s => s.SetProperty(h => h.FechaRegistro, noche));
        }
        using var gerente = await GerenteAsync();
        async Task<int> CantidadElAsync(DateOnly d) =>
            ZonaDe(await (await gerente.GetAsync($"/api/reportes/ventas-por-zona?desde={d:yyyy-MM-dd}&hasta={d:yyyy-MM-dd}"))
                .LeerJsonAsync(), zonaId).GetProperty("cantidad").GetInt32();

        Assert.Equal(1, await CantidadElAsync(dia));
        Assert.Equal(0, await CantidadElAsync(dia.AddDays(1)));
    }

    [Fact]
    public async Task Sin_cartera_completa_solo_ve_sus_zonas_y_sus_ventas()
    {
        using var operador = await api.OperadorAsync();
        using var admin = await api.AdminAsync();
        var (_, rol) = await admin.CrearRolAsync(Permisos.Reportes.VentasPorZona, Permisos.Territorial.RecorrerRuta);
        var usuario = await api.CrearUsuarioAsync(rol);
        var suya = await operador.CrearZonaAsync(usuario.Id);
        var ajena = await operador.CrearZonaAsync();
        var productoId = await operador.CrearProductoAsync(1m, stockInicial: 20);
        var dia = new DateOnly(2002, 3, 10);
        await MoverAsync(dia,
            await ConfirmadaAsync(operador, await operador.CrearClienteAsync(suya), productoId, 6, credito: false),
            await ConfirmadaAsync(operador, await operador.CrearClienteAsync(ajena), productoId, 9, credito: false));
        using var cliente = api.ClienteCon(usuario.Token);

        var reporte = await (await cliente.GetAsync($"/api/reportes/ventas-por-zona?desde={dia:yyyy-MM-dd}&hasta={dia:yyyy-MM-dd}"))
            .LeerJsonAsync();

        Assert.Equal([suya], reporte.GetProperty("zonas").EnumerateArray().Select(z => z.GetProperty("zonaId").GetInt32()));
        Assert.Equal(6m, reporte.GetProperty("total").GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Por_promotor_trae_cada_uno_con_sus_HP_y_el_total_y_filtra_por_promotor()
    {
        using var operador = await api.OperadorAsync();
        var productoId = await operador.CrearProductoAsync(1m, stockInicial: 100);
        var dia = new DateOnly(2002, 4, 10);
        async Task<(string Id, int[] Hojas)> PromotorConVentasAsync(params decimal[] montos)
        {
            var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
            using var vendedor = api.ClienteCon(promotor.Token);
            var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id));
            var hojas = new List<int>();
            foreach (var monto in montos) hojas.Add(await ConfirmadaAsync(vendedor, clienteId, productoId, monto, credito: true));
            return (promotor.Id, hojas.ToArray());
        }
        var (masVende, deUno) = await PromotorConVentasAsync(10, 20);
        var (menosVende, deOtro) = await PromotorConVentasAsync(5);
        await MoverAsync(dia, [.. deUno, .. deOtro]);
        using var gerente = await GerenteAsync();
        var periodo = $"desde={dia:yyyy-MM-dd}&hasta={dia:yyyy-MM-dd}";

        var todos = await (await gerente.GetAsync($"/api/reportes/ventas-por-promotor?{periodo}")).LeerJsonAsync();
        Assert.Equal([(masVende, 2, 30m), (menosVende, 1, 5m)], todos.GetProperty("promotores").EnumerateArray()
            .Select(p => (p.GetProperty("promotorId").GetString(), p.GetProperty("cantidad").GetInt32(), p.GetProperty("total").GetDecimal())));
        Assert.Equal((3, 35m), (todos.GetProperty("cantidad").GetInt32(), todos.GetProperty("total").GetDecimal()));

        var uno = await (await gerente.GetAsync($"/api/reportes/ventas-por-promotor?{periodo}&promotorId={menosVende}")).LeerJsonAsync();
        Assert.Equal(5m, uno.GetProperty("total").GetDecimal());
        Assert.Single(uno.GetProperty("promotores").EnumerateArray());
    }

    // Tarea 33: los dos reportes se exportan a Excel, con los mismos filtros y la misma cartera.

    private const string TipoExcel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    // La fila de la hoja cuyo primer valor es ese texto.
    private static IXLRow FilaDe(IXLWorksheet hoja, string texto) =>
        hoja.RowsUsed().Single(f => f.Cell(1).GetString() == texto);

    [Fact]
    public async Task El_Excel_por_zona_trae_las_zonas_tambien_en_cero_la_fila_Total_y_el_periodo()
    {
        using var operador = await api.OperadorAsync();
        var conVentas = await operador.CrearZonaAsync();
        var sinVentas = await operador.CrearZonaAsync();
        var clienteId = await operador.CrearClienteAsync(conVentas);
        var productoId = await operador.CrearProductoAsync(1m, stockInicial: 100);
        var dia = new DateOnly(2002, 5, 10);
        await MoverAsync(dia,
            await ConfirmadaAsync(operador, clienteId, productoId, 10, credito: false),
            await ConfirmadaAsync(operador, clienteId, productoId, 20, credito: true));
        using var gerente = await GerenteAsync();
        var periodo = $"desde={dia:yyyy-MM-dd}&hasta={dia:yyyy-MM-dd}";
        var json = await (await gerente.GetAsync($"/api/reportes/ventas-por-zona?{periodo}")).LeerJsonAsync();

        var respuesta = await gerente.GetAsync($"/api/reportes/ventas-por-zona/excel?{periodo}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(TipoExcel, respuesta.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("ventas_por_zona_", respuesta.Content.Headers.ContentDisposition?.FileNameStar);
        using var libro = new XLWorkbook(await respuesta.Content.ReadAsStreamAsync());
        var hoja = libro.Worksheet("Ventas por zona");
        Assert.Equal(["Zona", "HP", "Total", "Contado", "Crédito"], hoja.Row(1).Cells(1, 5).Select(c => c.GetString()));
        var conFila = FilaDe(hoja, ZonaDe(json, conVentas).GetProperty("zona").GetString()!);
        Assert.Equal((2, 30m, 10m, 20m), (conFila.Cell(2).GetValue<int>(), conFila.Cell(3).GetValue<decimal>(),
            conFila.Cell(4).GetValue<decimal>(), conFila.Cell(5).GetValue<decimal>()));
        var sinFila = FilaDe(hoja, ZonaDe(json, sinVentas).GetProperty("zona").GetString()!);
        Assert.Equal(0m, sinFila.Cell(3).GetValue<decimal>());
        var total = FilaDe(hoja, "Total");
        Assert.Equal((2, 30m), (total.Cell(2).GetValue<int>(), total.Cell(3).GetValue<decimal>()));
        Assert.Equal("10/05/2002 al 10/05/2002", FilaDe(hoja, "Período").Cell(2).GetString());
    }

    [Fact]
    public async Task El_Excel_por_promotor_trae_al_promotor_elegido_con_su_total()
    {
        using var operador = await api.OperadorAsync();
        var productoId = await operador.CrearProductoAsync(1m, stockInicial: 100);
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var vendedor = api.ClienteCon(promotor.Token);
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id));
        var dia = new DateOnly(2002, 6, 10);
        await MoverAsync(dia,
            await ConfirmadaAsync(vendedor, clienteId, productoId, 10, credito: true),
            await ConfirmadaAsync(vendedor, clienteId, productoId, 15, credito: false));
        using var gerente = await GerenteAsync();
        var periodo = $"desde={dia:yyyy-MM-dd}&hasta={dia:yyyy-MM-dd}";
        var nombre = (await (await gerente.GetAsync($"/api/reportes/ventas-por-promotor?{periodo}")).LeerJsonAsync())
            .GetProperty("promotores")[0].GetProperty("promotor").GetString()!;

        var respuesta = await gerente.GetAsync($"/api/reportes/ventas-por-promotor/excel?{periodo}&promotorId={promotor.Id}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.StartsWith("ventas_por_promotor_", respuesta.Content.Headers.ContentDisposition?.FileNameStar);
        using var libro = new XLWorkbook(await respuesta.Content.ReadAsStreamAsync());
        var hoja = libro.Worksheet("Ventas por promotor");
        var suya = FilaDe(hoja, nombre);
        Assert.Equal((2, 25m), (suya.Cell(2).GetValue<int>(), suya.Cell(3).GetValue<decimal>()));
        Assert.Equal(25m, FilaDe(hoja, "Total").Cell(3).GetValue<decimal>());
        // Debajo, el período y el promotor elegido ("Promotor" es también el encabezado de la primera columna).
        var ultima = hoja.LastRowUsed()!;
        Assert.Equal(("Promotor", nombre), (ultima.Cell(1).GetString(), ultima.Cell(2).GetString()));
    }

    [Fact]
    public async Task Sin_ventas_no_se_exporta_y_cada_Excel_pide_su_permiso()
    {
        using var gerente = await GerenteAsync();
        // Un día de 2002 sin ventas: las zonas salen en cero, y en cero no hay nada que exportar (ext. 3).
        const string vacio = "desde=2002-07-10&hasta=2002-07-10";
        await (await gerente.GetAsync($"/api/reportes/ventas-por-zona/excel?{vacio}")).ProblemaAsync(400, "reporte_sin_datos");
        await (await gerente.GetAsync($"/api/reportes/ventas-por-promotor/excel?{vacio}")).ProblemaAsync(400, "reporte_sin_datos");

        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);
        await (await secretaria.GetAsync("/api/reportes/ventas-por-zona/excel")).ProblemaAsync(403, "sin_permiso");
        await (await secretaria.GetAsync("/api/reportes/ventas-por-promotor/excel")).ProblemaAsync(403, "sin_permiso");
    }

    [Fact]
    public async Task Rechaza_un_rango_al_reves_y_cada_reporte_pide_su_permiso()
    {
        using var gerente = await GerenteAsync();
        await (await gerente.GetAsync("/api/reportes/ventas-por-zona?desde=2026-10-21&hasta=2026-10-20")).ProblemaAsync(400, "validacion");
        await (await gerente.GetAsync("/api/reportes/ventas-por-promotor?desde=2026-10-21&hasta=2026-10-20")).ProblemaAsync(400, "validacion");

        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await secretaria.GetAsync("/api/reportes/ventas-por-zona")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await secretaria.GetAsync("/api/reportes/ventas-por-promotor")).StatusCode);
    }
}
