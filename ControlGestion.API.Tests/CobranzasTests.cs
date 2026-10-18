using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class CobranzasTests(ApiDePrueba api)
{
    private record Escenario(HttpClient Admin, int ZonaId, int ProductoId) : IDisposable
    {
        public void Dispose() => Admin.Dispose();
    }

    private async Task<Escenario> PrepararAsync(decimal precio = 10m)
    {
        var admin = await api.AdminAsync();
        var zonaId = await admin.CrearZonaAsync();
        return new Escenario(admin, zonaId, await admin.CrearProductoAsync(precio));
    }

    private static async Task<List<int>> IdsDeCobranzasAsync(HttpClient cliente, string consulta = "") =>
        (await (await cliente.GetAsync($"/api/cobranzas{consulta}")).LeerJsonAsync())
            .EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).ToList();

    [Fact]
    public async Task Un_promotor_solo_ve_las_cobranzas_de_sus_clientes()
    {
        using var escenario = await PrepararAsync();
        var (admin, zonaId, productoId) = escenario;
        var promotorA = await api.CrearUsuarioAsync("Promotor");
        var promotorB = await api.CrearUsuarioAsync("Promotor");
        var deA = await admin.CrearCobranzaAsync(await admin.CrearClienteAsync(zonaId, promotorA.Id), zonaId, productoId, 1);
        var deB = await admin.CrearCobranzaAsync(await admin.CrearClienteAsync(zonaId, promotorB.Id), zonaId, productoId, 1);
        using var clienteA = api.ClienteCon(promotorA.Token);

        var visibles = await IdsDeCobranzasAsync(clienteA);

        Assert.Contains(deA, visibles);
        Assert.DoesNotContain(deB, visibles);
        Assert.Contains(deB, await IdsDeCobranzasAsync(admin));
    }

    [Fact]
    public async Task Un_promotor_no_ve_ni_paga_la_cobranza_de_un_cliente_ajeno()
    {
        using var escenario = await PrepararAsync();
        var (admin, zonaId, productoId) = escenario;
        var promotorA = await api.CrearUsuarioAsync("Promotor");
        var promotorB = await api.CrearUsuarioAsync("Promotor");
        var deB = await admin.CrearCobranzaAsync(await admin.CrearClienteAsync(zonaId, promotorB.Id), zonaId, productoId, 1);
        using var clienteA = api.ClienteCon(promotorA.Token);

        await (await clienteA.GetAsync($"/api/cobranzas/{deB}")).ProblemaAsync(404, "no_encontrado");
        await (await clienteA.PostAsJsonAsync($"/api/cobranzas/{deB}/pagos", new { monto = 1, metodoPago = 0 }))
            .ProblemaAsync(404, "no_encontrado");
        var cobranza = await (await admin.GetAsync($"/api/cobranzas/{deB}")).LeerJsonAsync();
        Assert.Equal(0, cobranza.GetProperty("pagos").GetArrayLength());
    }

    [Fact]
    public async Task Un_promotor_cobra_las_cobranzas_de_sus_clientes()
    {
        using var escenario = await PrepararAsync();
        var (admin, zonaId, productoId) = escenario;
        var promotor = await api.CrearUsuarioAsync("Promotor");
        var cobranzaId = await admin.CrearCobranzaAsync(await admin.CrearClienteAsync(zonaId, promotor.Id), zonaId, productoId, 1);
        using var cliente = api.ClienteCon(promotor.Token);

        var respuesta = await cliente.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos", new { monto = 4, metodoPago = 0 });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(4m, (await respuesta.LeerJsonAsync()).GetProperty("montoPagado").GetDecimal());
    }

    [Fact]
    public async Task El_responsable_de_la_cobranza_es_quien_vendio_aunque_la_confirme_otro()
    {
        using var escenario = await PrepararAsync();
        var (admin, zonaId, productoId) = escenario;
        var promotor = await api.CrearUsuarioAsync("Promotor");
        var clienteId = await admin.CrearClienteAsync(zonaId, promotor.Id);
        using var vendedor = api.ClienteCon(promotor.Token);
        var hojaId = await vendedor.CrearHojaPedidoAsync(clienteId, zonaId, productoId, 1);

        (await admin.PostAsync($"/api/hojas-pedido/{hojaId}/confirmar", null)).EnsureSuccessStatusCode();

        var cobranza = (await (await admin.GetAsync($"/api/cobranzas?clienteId={clienteId}")).LeerJsonAsync())
            .EnumerateArray().Single();
        Assert.Equal(promotor.Id, cobranza.GetProperty("promotor").GetProperty("id").GetString());
    }

    [Fact]
    public async Task El_filtro_por_dia_usa_el_dia_de_Bolivia()
    {
        using var escenario = await PrepararAsync();
        var (admin, zonaId, productoId) = escenario;
        var clienteId = await admin.CrearClienteAsync(zonaId);
        var cobranzaId = await admin.CrearCobranzaAsync(clienteId, zonaId, productoId, 1);
        var hoyEnBolivia = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-4));

        var deHoy = await IdsDeCobranzasAsync(admin, $"?clienteId={clienteId}&desde={hoyEnBolivia:yyyy-MM-dd}&hasta={hoyEnBolivia:yyyy-MM-dd}");
        var deManana = await IdsDeCobranzasAsync(admin, $"?clienteId={clienteId}&desde={hoyEnBolivia.AddDays(1):yyyy-MM-dd}");

        Assert.Equal([cobranzaId], deHoy);
        Assert.Empty(deManana);
    }

    [Fact]
    public async Task Marcar_incobrable_exige_una_observacion()
    {
        using var escenario = await PrepararAsync();
        var (admin, zonaId, productoId) = escenario;
        var cobranzaId = await admin.CrearCobranzaAsync(await admin.CrearClienteAsync(zonaId), zonaId, productoId, 1);

        var problema = await (await admin.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/incobrable", new { observacion = "" }))
            .ProblemaAsync(400, "validacion");
        Assert.True(problema.GetProperty("errors").TryGetProperty("observacion", out _));

        var marcada = await (await admin.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/incobrable",
            new { observacion = "El cliente cerró su negocio" })).LeerJsonAsync();
        Assert.Equal(4, marcada.GetProperty("estado").GetInt32());
        Assert.Equal("El cliente cerró su negocio", marcada.GetProperty("observaciones").GetString());
    }
}
