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

        // Los 5 sembrados van primero, por su id; después, los activos que registran otras pruebas (tarea 32), en cero.
        var porTipo = reporte.GetProperty("porTipo").EnumerateArray()
            .Select(t => (t.GetProperty("tipoEventoId").GetInt32(), t.GetProperty("cantidad").GetInt32())).ToList();
        Assert.Equal([(1, 2), (2, 0), (3, 1), (4, 0), (5, 0)], porTipo.Take(5));
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
        Assert.Equal("Cliente ausente", eventos.Cell(2, 3).GetString());
        Assert.Equal("Media", eventos.Cell(2, 4).GetString());
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
        await (await secretaria.GetAsync("/api/eventos/reporte/pdf")).ProblemaAsync(403, "sin_permiso");
    }

    // Tarea 33: el PDF, con los mismos filtros, la misma cartera y la misma regla de "sin datos".

    private static async Task<string> NumeroAsync(HttpClient cliente, int eventoId) =>
        (await (await cliente.GetAsync($"/api/eventos/{eventoId}")).LeerJsonAsync()).GetProperty("numero").GetString()!;

    [Fact]
    public async Task El_PDF_trae_los_totales_y_los_eventos_del_filtro_con_la_hora_de_Bolivia()
    {
        using var operador = await api.OperadorAsync();
        var (clienteId, zonaId, resuelto) = await TresEventosAsync(operador);
        using (var scope = api.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Eventos.Where(e => e.Id == resuelto)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.FechaEvento, new DateTime(2026, 5, 16, 2, 0, 0, DateTimeKind.Utc)));
        var deOtraZona = await operador.CrearEventoAsync(await operador.CrearClienteAsync(await operador.CrearZonaAsync()));
        using var gerente = api.ClienteCon((await api.CrearUsuarioAsync(Roles.GerenteGeneral)).Token);

        var texto = await (await gerente.GetAsync($"/api/eventos/reporte/pdf?zonaId={zonaId}")).TextoDelPdfAsync("incidencias");

        Assert.Contains("Reporte de incidencias", texto);
        Assert.Contains("Zona: Zona Z-", texto);
        Assert.Contains("Total 3", texto);
        Assert.Contains($"{await NumeroAsync(operador, resuelto)} 15/05/2026 22:00 Cliente ausente", texto);
        Assert.DoesNotContain(await NumeroAsync(operador, deOtraZona), texto);
    }

    [Fact]
    public async Task El_PDF_respeta_la_cartera_y_sin_eventos_no_se_exporta()
    {
        using var operador = await api.OperadorAsync();
        using var admin = await api.AdminAsync();
        var (_, rol) = await admin.CrearRolAsync(Permisos.Eventos.GenerarReporte, Permisos.Territorial.RecorrerRuta);
        var usuario = await api.CrearUsuarioAsync(rol);
        var suyo = await operador.CrearEventoAsync(await operador.CrearClienteAsync(await operador.CrearZonaAsync(usuario.Id)));
        var ajeno = await operador.CrearEventoAsync(await operador.CrearClienteAsync(await operador.CrearZonaAsync()));
        using var sinCartera = api.ClienteCon(usuario.Token);

        var texto = await (await sinCartera.GetAsync("/api/eventos/reporte/pdf")).TextoDelPdfAsync("incidencias");

        Assert.Contains(await NumeroAsync(operador, suyo), texto);
        Assert.DoesNotContain(await NumeroAsync(operador, ajeno), texto);
        using var gerente = api.ClienteCon((await api.CrearUsuarioAsync(Roles.GerenteGeneral)).Token);
        await (await gerente.GetAsync("/api/eventos/reporte/pdf?desde=2001-01-01&hasta=2001-01-01")).ProblemaAsync(400, "reporte_sin_datos");
    }
}
