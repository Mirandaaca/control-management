using System.Net.Http.Json;
using ClosedXML.Excel;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>El reporte de incidencias (4.4), del gerente general.</summary>
[Collection(ColeccionApi.Nombre)]
public class ReporteEventosTests(ApiDePrueba api)
{
    /// <summary>Un cliente con tres eventos: dos de tipo 1 (uno resuelto, con foto) y uno de tipo 3.</summary>
    private static async Task<(int ClienteId, int ZonaId, int Resuelto)> TresEventosAsync(HttpClient operador)
    {
        var zonaId = await operador.CrearZonaAsync();
        var clienteId = await operador.CrearClienteAsync(zonaId);
        var resuelto = await operador.CrearEventoAsync(clienteId, tipoEventoId: 1);
        (await operador.SubirFotoAsync(resuelto)).EnsureSuccessStatusCode();
        (await operador.PostAsJsonAsync($"/api/eventos/{resuelto}/resolver", new { solucion = "Se volvió a visitar" }))
            .EnsureSuccessStatusCode();
        await operador.CrearEventoAsync(clienteId, tipoEventoId: 1);
        await operador.CrearEventoAsync(clienteId, tipoEventoId: 3);
        return (clienteId, zonaId, resuelto);
    }

    [Fact]
    public async Task Cuenta_los_eventos_por_tipo_y_por_estado_tambien_los_que_estan_en_cero()
    {
        using var operador = await api.OperadorAsync();
        var (_, zonaId, _) = await TresEventosAsync(operador);
        using var gerente = api.ClienteCon((await api.CrearUsuarioAsync(Roles.GerenteGeneral)).Token);

        var reporte = await (await gerente.GetAsync($"/api/eventos/reporte?zonaId={zonaId}&tamano=2")).LeerJsonAsync();

        var porTipo = reporte.GetProperty("porTipo").EnumerateArray()
            .Select(t => (t.GetProperty("tipoEventoId").GetInt32(), t.GetProperty("cantidad").GetInt32())).ToList();
        Assert.Equal([(1, 2), (2, 0), (3, 1), (4, 0), (5, 0)], porTipo);
        var porEstado = reporte.GetProperty("porEstado").EnumerateArray()
            .Select(e => (e.GetProperty("nombre").GetString(), e.GetProperty("cantidad").GetInt32())).ToList();
        Assert.Equal([("Pendiente", 2), ("Resuelto", 1)], porEstado);
        Assert.Equal(3, reporte.GetProperty("cantidad").GetInt32());
        Assert.Equal(2, reporte.GetProperty("eventos").GetProperty("items").GetArrayLength());
        Assert.Equal(2, reporte.GetProperty("eventos").GetProperty("totalPaginas").GetInt32());
    }

    [Fact]
    public async Task Rechaza_un_rango_al_reves_y_sin_eventos_no_exporta()
    {
        using var gerente = api.ClienteCon((await api.CrearUsuarioAsync(Roles.GerenteGeneral)).Token);

        await (await gerente.GetAsync("/api/eventos/reporte?desde=2026-10-21&hasta=2026-10-20")).ProblemaAsync(400, "validacion");
        await (await gerente.GetAsync("/api/eventos/reporte/excel?desde=2001-01-01&hasta=2001-01-01"))
            .ProblemaAsync(400, "reporte_sin_datos");
    }

    [Fact]
    public async Task El_Excel_trae_los_eventos_con_la_hora_de_Bolivia_y_los_totales()
    {
        using var operador = await api.OperadorAsync();
        var (clienteId, _, resuelto) = await TresEventosAsync(operador);
        // Las 22:00 del 15 en Santa Cruz son las 02:00 del 16 en UTC.
        using (var scope = api.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Eventos.Where(e => e.Id == resuelto)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.FechaEvento, new DateTime(2026, 5, 16, 2, 0, 0, DateTimeKind.Utc)));
        using var gerente = api.ClienteCon((await api.CrearUsuarioAsync(Roles.GerenteGeneral)).Token);

        var respuesta = await gerente.GetAsync($"/api/eventos/reporte/excel?clienteId={clienteId}");

        respuesta.EnsureSuccessStatusCode();
        Assert.Equal($"incidencias_{HoraBolivia.Hoy(TimeProvider.System):yyyyMMdd}.xlsx",
            respuesta.Content.Headers.ContentDisposition?.FileName);
        using var libro = new XLWorkbook(await respuesta.Content.ReadAsStreamAsync());
        var eventos = libro.Worksheet("Eventos");
        Assert.Equal("Número", eventos.Cell(1, 1).GetString());
        Assert.Equal(new DateTime(2026, 5, 15, 22, 0, 0), eventos.Cell(2, 2).GetDateTime());
        Assert.Equal("Cliente Ausente", eventos.Cell(2, 3).GetString());
        Assert.Equal("Resuelto", eventos.Cell(2, 5).GetString());
        Assert.Equal(1, eventos.Cell(2, 11).GetValue<int>());
        Assert.Equal("Se volvió a visitar", eventos.Cell(2, 12).GetString());
        Assert.Equal(4, eventos.LastRowUsed()!.RowNumber());
        var totales = libro.Worksheet("Totales");
        Assert.Equal("Total", totales.LastRowUsed()!.Cell(1).GetString());
        Assert.Equal(3, totales.LastRowUsed()!.Cell(2).GetValue<int>());
    }

    [Fact]
    public async Task Solo_lo_genera_quien_tiene_el_permiso()
    {
        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);

        await (await secretaria.GetAsync("/api/eventos/reporte")).ProblemaAsync(403, "sin_permiso");
        await (await secretaria.GetAsync("/api/eventos/reporte/excel")).ProblemaAsync(403, "sin_permiso");
    }
}
