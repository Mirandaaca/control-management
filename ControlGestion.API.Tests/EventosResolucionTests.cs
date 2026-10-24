using System.Net.Http.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class EventosResolucionTests(ApiDePrueba api)
{
    private const int Pendiente = 0;
    private const int Resuelto = 1;

    private static Task<HttpResponseMessage> ResolverAsync(HttpClient cliente, int eventoId, string solucion = "Se habló con el cliente") =>
        cliente.PostAsJsonAsync($"/api/eventos/{eventoId}/resolver", new { solucion });

    /// <summary>Un evento Pendiente registrado por un promotor, y la secretaria que lo resuelve.</summary>
    private async Task<(int EventoId, UsuarioDePrueba Promotor, HttpClient Secretaria)> EventoPendienteAsync()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id));
        using var clientePromotor = api.ClienteCon(promotor.Token);
        var eventoId = await clientePromotor.CrearEventoAsync(clienteId);
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        return (eventoId, promotor, api.ClienteCon(secretaria.Token));
    }

    [Fact]
    public async Task Resolver_guarda_la_solucion_la_fecha_y_quien_lo_resolvio()
    {
        var (eventoId, _, secretaria) = await EventoPendienteAsync();
        using var _s = secretaria;
        var antes = DateTime.UtcNow.AddSeconds(-5);

        var evento = await (await ResolverAsync(secretaria, eventoId, "  Se reprogramó la visita  ")).LeerJsonAsync();

        Assert.Equal(Resuelto, evento.GetProperty("estado").GetInt32());
        Assert.Equal("Se reprogramó la visita", evento.GetProperty("solucion").GetString());
        Assert.Equal("Prueba Secretaria", evento.GetProperty("resueltoPor").GetProperty("nombreCompleto").GetString());
        Assert.InRange(DateTime.SpecifyKind(evento.GetProperty("fechaResolucion").GetDateTime(), DateTimeKind.Utc),
            antes, DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task Sin_solucion_no_se_resuelve_y_un_resuelto_no_se_resuelve_otra_vez()
    {
        var (eventoId, _, secretaria) = await EventoPendienteAsync();
        using var _s = secretaria;

        var sinSolucion = await (await ResolverAsync(secretaria, eventoId, "")).ProblemaAsync(400, "validacion");
        Assert.True(sinSolucion.GetProperty("errors").TryGetProperty("solucion", out _));

        (await ResolverAsync(secretaria, eventoId)).EnsureSuccessStatusCode();
        await (await ResolverAsync(secretaria, eventoId, "Otra solución")).ProblemaAsync(400, "evento_resuelto");
    }

    [Fact]
    public async Task La_lista_filtra_por_estado()
    {
        var (eventoId, promotor, secretaria) = await EventoPendienteAsync();
        using var _s = secretaria;
        using var clientePromotor = api.ClienteCon(promotor.Token);
        var clienteId = (await (await clientePromotor.GetAsync($"/api/eventos/{eventoId}")).LeerJsonAsync())
            .GetProperty("cliente").GetProperty("id").GetInt32();
        var otroId = await clientePromotor.CrearEventoAsync(clienteId);
        (await ResolverAsync(secretaria, eventoId)).EnsureSuccessStatusCode();

        async Task<List<int>> IdsAsync(int estado) =>
            (await (await secretaria.GetAsync($"/api/eventos?clienteId={clienteId}&estado={estado}")).LeerJsonAsync())
                .GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToList();

        Assert.Equal([otroId], await IdsAsync(Pendiente));
        Assert.Equal([eventoId], await IdsAsync(Resuelto));
    }

    [Fact]
    public async Task El_tablero_cuenta_los_eventos_pendientes()
    {
        using var operador = await api.OperadorAsync();
        async Task<int> PendientesAsync() =>
            (await (await operador.GetAsync("/api/reportes/tablero")).LeerJsonAsync()).GetProperty("eventosPendientes").GetInt32();
        var antes = await PendientesAsync();

        var eventoId = await operador.CrearEventoAsync(await operador.CrearClienteAsync(await operador.CrearZonaAsync()));
        Assert.Equal(antes + 1, await PendientesAsync());

        (await ResolverAsync(operador, eventoId)).EnsureSuccessStatusCode();
        Assert.Equal(antes, await PendientesAsync());
    }
}
