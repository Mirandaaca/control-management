using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class EventosTests(ApiDePrueba api)
{
    private static async Task<List<int>> IdsAsync(HttpClient cliente, string consulta = "") =>
        (await (await cliente.GetAsync($"/api/eventos{consulta}")).LeerJsonAsync())
            .GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToList();

    [Fact]
    public async Task Registrar_numera_el_evento()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());

        var respuesta = await operador.PostAsJsonAsync("/api/eventos", new
        {
            tipoEventoId = 3,
            clienteId,
            latitud = -17.7833m,
            longitud = -63.1821m,
            descripcion = "El portón estaba cerrado"
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var evento = await respuesta.LeerJsonAsync();
        Assert.Matches(new Regex($@"^EVT-{DateTime.UtcNow.AddHours(-4).Year}-\d{{5}}$"), evento.GetProperty("numero").GetString());
        var id = evento.GetProperty("id").GetInt32();
        Assert.Equal(id, (await (await operador.GetAsync($"/api/eventos/{id}")).LeerJsonAsync()).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Un_evento_que_no_existe_responde_404()
    {
        using var operador = await api.OperadorAsync();

        await (await operador.GetAsync("/api/eventos/999999")).ProblemaAsync(404, "no_encontrado");
    }

    [Fact]
    public async Task El_catalogo_trae_los_cinco_tipos_y_cuales_piden_evidencia()
    {
        using var operador = await api.OperadorAsync();

        var tipos = (await (await operador.GetAsync("/api/eventos/tipos")).LeerJsonAsync()).EnumerateArray()
            .Select(t => (t.GetProperty("id").GetInt32(), t.GetProperty("requiereEvidencia").GetBoolean())).ToList();

        Assert.Equal([(1, true), (2, true), (3, false), (4, false), (5, false)], tipos);
    }

    [Fact]
    public async Task El_promotor_no_resuelve_y_la_secretaria_no_registra()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id));
        using var clientePromotor = api.ClienteCon(promotor.Token);
        using var clienteSecretaria = api.ClienteCon(secretaria.Token);
        var eventoId = await clientePromotor.CrearEventoAsync(clienteId);

        await (await clientePromotor.PostAsJsonAsync($"/api/eventos/{eventoId}/resolver", new { solucion = "Listo" }))
            .ProblemaAsync(403, "sin_permiso");
        await (await clienteSecretaria.PostAsJsonAsync("/api/eventos", new
        {
            tipoEventoId = 3, clienteId, latitud = -17.7833m, longitud = -63.1821m, descripcion = "No debería entrar"
        })).ProblemaAsync(403, "sin_permiso");
    }

    [Fact]
    public async Task Un_promotor_ve_los_que_registro_los_de_su_cartera_y_los_de_sus_pedidos_asignados()
    {
        using var operador = await api.OperadorAsync();
        var promotorA = await api.CrearUsuarioAsync(Roles.Promotor);
        var promotorB = await api.CrearUsuarioAsync(Roles.Promotor);
        var clienteDeA = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotorA.Id));
        var clienteDeB = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotorB.Id));
        var pedidoDeB = await operador.CrearPedidoAsync(clienteDeB, await operador.CrearProductoAsync(10m), 1);
        (await operador.PostAsJsonAsync("/api/pedidos/asignar", new
        {
            pedidoIds = new[] { pedidoDeB },
            promotorId = promotorA.Id,
            dia = HoraBolivia.Hoy(TimeProvider.System).AddDays(1).ToString("yyyy-MM-dd")
        })).EnsureSuccessStatusCode();
        using var clienteA = api.ClienteCon(promotorA.Token);
        using var clienteB = api.ClienteCon(promotorB.Token);

        var registradoPorA = await clienteA.CrearEventoAsync(clienteDeA);
        var deSuCartera = await operador.CrearEventoAsync(clienteDeA);
        var deB = await clienteB.CrearEventoAsync(clienteDeB);
        var respuesta = await operador.PostAsJsonAsync("/api/eventos", new
        {
            tipoEventoId = 2, clienteId = clienteDeB, pedidoId = pedidoDeB, latitud = -17.7833m, longitud = -63.1821m,
            descripcion = "No había nadie para recibir"
        });
        var delPedidoAsignado = (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt32();

        var visiblesParaA = await IdsAsync(clienteA);
        Assert.Equal([delPedidoAsignado, deSuCartera, registradoPorA], visiblesParaA);
        await (await clienteA.GetAsync($"/api/eventos/{deB}")).ProblemaAsync(404, "no_encontrado");
        Assert.Equal([delPedidoAsignado, deB], await IdsAsync(clienteB));
    }

    [Fact]
    public async Task La_lista_filtra_por_tipo_zona_promotor_y_busqueda_y_viene_paginada()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var zonaId = await operador.CrearZonaAsync(promotor.Id);
        var clienteId = await operador.CrearClienteAsync(zonaId);
        using var cliente = api.ClienteCon(promotor.Token);
        var ausente = await cliente.CrearEventoAsync(clienteId, tipoEventoId: 1);
        var acceso = await cliente.CrearEventoAsync(clienteId, tipoEventoId: 3);
        var otro = await cliente.CrearEventoAsync(clienteId, tipoEventoId: 5);
        var descripcion = (await (await operador.GetAsync($"/api/eventos/{acceso}")).LeerJsonAsync())
            .GetProperty("descripcion").GetString()!;

        Assert.Equal([ausente], await IdsAsync(operador, $"?zonaId={zonaId}&tipoEventoId=1"));
        Assert.Equal([otro, acceso, ausente], await IdsAsync(operador, $"?promotorId={promotor.Id}"));
        Assert.Equal([acceso], await IdsAsync(operador, $"?buscar={Uri.EscapeDataString(descripcion)}"));

        var pagina = await (await operador.GetAsync($"/api/eventos?clienteId={clienteId}&tamano=2&pagina=2")).LeerJsonAsync();
        Assert.Equal(3, pagina.GetProperty("total").GetInt32());
        Assert.Equal(2, pagina.GetProperty("totalPaginas").GetInt32());
        Assert.Equal(ausente, Assert.Single(pagina.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Las_fechas_se_filtran_por_dia_de_Bolivia()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var eventoId = await operador.CrearEventoAsync(clienteId);
        // Las 22:00 del 15 en Santa Cruz son las 02:00 del 16 en UTC: el evento es del día 15.
        using (var scope = api.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Eventos.Where(e => e.Id == eventoId)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.FechaEvento, new DateTime(2030, 5, 16, 2, 0, 0, DateTimeKind.Utc)));

        Assert.Equal([eventoId], await IdsAsync(operador, $"?clienteId={clienteId}&desde=2030-05-15&hasta=2030-05-15"));
        Assert.Empty(await IdsAsync(operador, $"?clienteId={clienteId}&desde=2030-05-16"));
        await (await operador.GetAsync("/api/eventos?desde=2030-05-16&hasta=2030-05-15")).ProblemaAsync(400, "validacion");
    }
}
