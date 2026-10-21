using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class ReportePedidosTests(ApiDePrueba api)
{
    private record Escenario(int ZonaId, UsuarioDePrueba Promotor, int Pendiente, int EnProceso, int Anulado);

    /// <summary>En una zona propia: un pedido pendiente de 10, uno en proceso de 20 y uno anulado de 30.</summary>
    private async Task<Escenario> TresPedidosAsync(HttpClient operador)
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var zonaId = await operador.CrearZonaAsync(promotor.Id);
        var clienteId = await operador.CrearClienteAsync(zonaId);
        var productoId = await operador.CrearProductoAsync(10m);
        var pendiente = await operador.CrearPedidoAsync(clienteId, productoId, 1);
        var enProceso = await operador.CrearPedidoAsync(clienteId, productoId, 2);
        var anulado = await operador.CrearPedidoAsync(clienteId, productoId, 3);
        (await operador.PostAsJsonAsync($"/api/pedidos/{enProceso}/avanzar", new { })).EnsureSuccessStatusCode();
        (await operador.PostAsJsonAsync($"/api/pedidos/{anulado}/anular", new { motivo = "Prueba" })).EnsureSuccessStatusCode();
        return new Escenario(zonaId, promotor, pendiente, enProceso, anulado);
    }

    private static async Task<JsonElement> ReporteAsync(HttpClient cliente, string consulta) =>
        await (await cliente.GetAsync($"/api/pedidos/reporte{consulta}")).LeerJsonAsync();

    private static List<int> IdsDe(JsonElement reporte) =>
        reporte.GetProperty("pedidos").GetProperty("items").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).ToList();

    [Fact]
    public async Task El_reporte_da_los_cinco_estados_con_su_cantidad_y_su_monto_y_el_total_sin_los_anulados()
    {
        using var operador = await api.OperadorAsync();
        var e = await TresPedidosAsync(operador);
        using var gerente = api.ClienteCon((await api.CrearUsuarioAsync(Roles.GerenteGeneral)).Token);

        var reporte = await ReporteAsync(gerente, $"?zonaId={e.ZonaId}");

        var porEstado = reporte.GetProperty("porEstado").EnumerateArray()
            .Select(t => (t.GetProperty("estado").GetString(), t.GetProperty("cantidad").GetInt32(), t.GetProperty("monto").GetDecimal()))
            .ToList();
        Assert.Equal([("Pendiente", 1, 10m), ("En Proceso", 1, 20m), ("En Ruta", 0, 0m), ("Entregado", 0, 0m), ("Anulado", 1, 30m)],
            porEstado);
        Assert.Equal(2, reporte.GetProperty("cantidad").GetInt32());
        Assert.Equal(30m, reporte.GetProperty("monto").GetDecimal());
        Assert.Equal(3, reporte.GetProperty("pedidos").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task El_reporte_filtra_por_estado_promotor_y_dias_de_entrega()
    {
        using var operador = await api.OperadorAsync();
        var e = await TresPedidosAsync(operador);
        var otroPromotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var gerente = api.ClienteCon((await api.CrearUsuarioAsync(Roles.GerenteGeneral)).Token);
        var manana = HoraBolivia.Hoy(TimeProvider.System).AddDays(1);

        Assert.Equal([e.Anulado], IdsDe(await ReporteAsync(gerente, $"?zonaId={e.ZonaId}&estadoId=5")));
        Assert.Equal(3, IdsDe(await ReporteAsync(gerente, $"?promotorId={e.Promotor.Id}")).Count);
        Assert.Empty(IdsDe(await ReporteAsync(gerente, $"?zonaId={e.ZonaId}&promotorId={otroPromotor.Id}")));
        Assert.Equal(3, IdsDe(await ReporteAsync(gerente, $"?zonaId={e.ZonaId}&desde={manana:yyyy-MM-dd}&hasta={manana:yyyy-MM-dd}")).Count);
        var deOtroDia = await ReporteAsync(gerente, $"?zonaId={e.ZonaId}&desde={manana.AddDays(1):yyyy-MM-dd}");
        Assert.Empty(IdsDe(deOtroDia));
        Assert.Equal(0, deOtroDia.GetProperty("cantidad").GetInt32());
    }

    [Fact]
    public async Task El_Excel_trae_los_pedidos_con_la_hora_de_Bolivia_y_los_totales()
    {
        using var operador = await api.OperadorAsync();
        var e = await TresPedidosAsync(operador);
        using var gerente = api.ClienteCon((await api.CrearUsuarioAsync(Roles.GerenteGeneral)).Token);

        var respuesta = await gerente.GetAsync($"/api/pedidos/reporte/excel?zonaId={e.ZonaId}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.EndsWith(".xlsx", respuesta.Content.Headers.ContentDisposition?.FileNameStar ?? respuesta.Content.Headers.ContentDisposition?.FileName);
        using var libro = new XLWorkbook(await respuesta.Content.ReadAsStreamAsync());

        var pedidos = libro.Worksheet("Pedidos");
        Assert.Equal("Número", pedidos.Cell(1, 1).GetString());
        Assert.Equal(4, pedidos.LastRowUsed()!.RowNumber());
        // Ordenados por entrega: los tres son de mañana a las 10:00 de Bolivia, sin el desfase de UTC.
        var manana = HoraBolivia.Hoy(TimeProvider.System).AddDays(1);
        Assert.Equal(manana.ToDateTime(new TimeOnly(10, 0)), pedidos.Cell(2, 8).GetDateTime());
        Assert.Equal(10m, (decimal)pedidos.Cell(2, 13).GetDouble());
        // Un pedido sin asignar deja la celda vacía.
        Assert.True(pedidos.Cell(2, 6).IsEmpty());

        var totales = libro.Worksheet("Totales por estado");
        Assert.Equal("Total sin anulados", totales.Cell(7, 1).GetString());
        Assert.Equal(2, (int)totales.Cell(7, 2).GetDouble());
        Assert.Equal(30m, (decimal)totales.Cell(7, 3).GetDouble());
    }

    [Fact]
    public async Task Solo_quien_genera_el_reporte_lo_ve_y_puede_filtrar_por_promotor()
    {
        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);
        using var admin = await api.AdminAsync();
        var (_, rol) = await admin.CrearRolAsync(Permisos.Pedidos.GenerarReporte);
        using var soloReporte = api.ClienteCon((await api.CrearUsuarioAsync(rol)).Token);

        await (await secretaria.GetAsync("/api/pedidos/reporte")).ProblemaAsync(403, "sin_permiso");
        await (await secretaria.GetAsync("/api/pedidos/reporte/excel")).ProblemaAsync(403, "sin_permiso");
        Assert.Equal(HttpStatusCode.OK, (await soloReporte.GetAsync("/api/pedidos/reporte")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await soloReporte.GetAsync("/api/usuarios/promotores")).StatusCode);
    }
}
