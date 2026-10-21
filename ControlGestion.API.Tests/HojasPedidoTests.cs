using System.Text.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class HojasPedidoTests(ApiDePrueba api)
{
    private static async Task<List<int>> IdsAsync(HttpClient cliente, string consulta = "") =>
        (await (await cliente.GetAsync($"/api/hojas-pedido{consulta}")).LeerJsonAsync())
            .GetProperty("items").EnumerateArray().Select(h => h.GetProperty("id").GetInt32()).ToList();

    private static async Task<JsonElement> HojaDe(HttpClient cliente, int hojaId) =>
        await (await cliente.GetAsync($"/api/hojas-pedido/{hojaId}")).LeerJsonAsync();

    /// <summary>Dos promotores, cada uno con una zona, un cliente y una HP de ese cliente.</summary>
    private async Task<(UsuarioDePrueba PromotorA, int HojaDeA, int HojaDeB)> DosCarterasAsync(HttpClient operador)
    {
        var productoId = await operador.CrearProductoAsync(10m);
        var promotorA = await api.CrearUsuarioAsync(Roles.Promotor);
        var promotorB = await api.CrearUsuarioAsync(Roles.Promotor);
        var zonaA = await operador.CrearZonaAsync(promotorA.Id);
        var zonaB = await operador.CrearZonaAsync(promotorB.Id);
        var hojaDeA = await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(zonaA), zonaA, productoId, 1);
        var hojaDeB = await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(zonaB), zonaB, productoId, 1);
        return (promotorA, hojaDeA, hojaDeB);
    }

    [Fact]
    public async Task Un_promotor_solo_ve_las_HP_de_los_clientes_de_su_cartera()
    {
        using var operador = await api.OperadorAsync();
        var (promotorA, hojaDeA, hojaDeB) = await DosCarterasAsync(operador);
        using var clienteA = api.ClienteCon(promotorA.Token);

        var visibles = await IdsAsync(clienteA);

        Assert.Equal([hojaDeA], visibles);
        Assert.Contains(hojaDeB, await IdsAsync(operador, $"?buscar={(await HojaDe(operador, hojaDeB)).GetProperty("numero").GetString()}"));
    }

    [Fact]
    public async Task Una_HP_ajena_responde_404_al_verla_y_al_confirmarla()
    {
        using var operador = await api.OperadorAsync();
        var (promotorA, _, hojaDeB) = await DosCarterasAsync(operador);
        using var clienteA = api.ClienteCon(promotorA.Token);

        await (await clienteA.GetAsync($"/api/hojas-pedido/{hojaDeB}")).ProblemaAsync(404, "no_encontrado");
        await (await clienteA.PostAsync($"/api/hojas-pedido/{hojaDeB}/confirmar", null)).ProblemaAsync(404, "no_encontrado");
        Assert.Equal(0, (await HojaDe(operador, hojaDeB)).GetProperty("estadoHP").GetInt32());
    }

    [Fact]
    public async Task El_filtro_por_dia_usa_el_dia_de_Bolivia()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var clienteId = await operador.CrearClienteAsync(zonaId);
        var hojaId = await operador.CrearHojaPedidoAsync(clienteId, zonaId, await operador.CrearProductoAsync(10m), 1);
        var hoyEnBolivia = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-4));

        var deHoy = await IdsAsync(operador, $"?clienteId={clienteId}&desde={hoyEnBolivia:yyyy-MM-dd}&hasta={hoyEnBolivia:yyyy-MM-dd}");
        var deManana = await IdsAsync(operador, $"?clienteId={clienteId}&desde={hoyEnBolivia.AddDays(1):yyyy-MM-dd}");

        Assert.Equal([hojaId], deHoy);
        Assert.Empty(deManana);
    }

    [Fact]
    public async Task Desde_posterior_a_hasta_responde_400()
    {
        using var operador = await api.OperadorAsync();

        var problema = await (await operador.GetAsync("/api/hojas-pedido?desde=2026-10-21&hasta=2026-10-20")).ProblemaAsync(400, "validacion");

        Assert.True(problema.GetProperty("errors").TryGetProperty("desde", out _));
    }

    [Fact]
    public async Task La_busqueda_encuentra_la_HP_por_su_numero()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var hojaId = await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(zonaId), zonaId,
            await operador.CrearProductoAsync(10m), 2);
        var hoja = await HojaDe(operador, hojaId);

        var encontradas = await IdsAsync(operador, $"?buscar={hoja.GetProperty("numero").GetString()}");

        Assert.Equal([hojaId], encontradas);
        var linea = Assert.Single(hoja.GetProperty("detalles").EnumerateArray());
        Assert.Equal(2m, linea.GetProperty("cantidad").GetDecimal());
        Assert.StartsWith("Producto P-", linea.GetProperty("descripcion").GetString());
    }
}
