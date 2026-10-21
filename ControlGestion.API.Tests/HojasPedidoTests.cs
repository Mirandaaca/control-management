using System.Net.Http.Json;
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

    private static Task<HttpResponseMessage> RegistrarAsync(HttpClient cliente, int clienteId, int productoId,
        decimal? latitud = -17.7833m, decimal? longitud = -63.1821m, int tipoVenta = 0, int? zonaId = null) =>
        cliente.PostAsJsonAsync("/api/hojas-pedido", new
        {
            clienteId, zonaId, latitud, longitud, tipoVenta,
            detalles = new[] { new { productoId, cantidad = 1 } }
        });

    /// <summary>Dos promotores, cada uno con una zona, un cliente y una HP de ese cliente.</summary>
    private async Task<(UsuarioDePrueba PromotorA, int HojaDeA, int HojaDeB)> DosCarterasAsync(HttpClient operador)
    {
        var productoId = await operador.CrearProductoAsync(10m);
        var promotorA = await api.CrearUsuarioAsync(Roles.Promotor);
        var promotorB = await api.CrearUsuarioAsync(Roles.Promotor);
        var zonaA = await operador.CrearZonaAsync(promotorA.Id);
        var zonaB = await operador.CrearZonaAsync(promotorB.Id);
        var hojaDeA = await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(zonaA), productoId, 1);
        var hojaDeB = await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(zonaB), productoId, 1);
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
        var hojaId = await operador.CrearHojaPedidoAsync(clienteId, await operador.CrearProductoAsync(10m), 1);
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
    public async Task Un_promotor_no_registra_HP_para_un_cliente_fuera_de_su_cartera()
    {
        using var operador = await api.OperadorAsync();
        var productoId = await operador.CrearProductoAsync(10m);
        var promotorA = await api.CrearUsuarioAsync(Roles.Promotor);
        var promotorB = await api.CrearUsuarioAsync(Roles.Promotor);
        var clienteDeB = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotorB.Id));
        using var clienteA = api.ClienteCon(promotorA.Token);

        // Responde igual que si el cliente no existiera: no se le confirma que existe.
        await (await RegistrarAsync(clienteA, clienteDeB, productoId)).ProblemaAsync(400, "cliente_no_existe");
        await (await RegistrarAsync(clienteA, 999999, productoId)).ProblemaAsync(400, "cliente_no_existe");
    }

    [Fact]
    public async Task Un_cliente_inactivo_no_recibe_HP()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        (await operador.PutAsJsonAsync($"/api/clientes/{clienteId}/estado", new { estadoClienteId = 4 })).EnsureSuccessStatusCode();

        await (await RegistrarAsync(operador, clienteId, await operador.CrearProductoAsync(10m))).ProblemaAsync(400, "cliente_inactivo");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0.0, 0.0)]
    public async Task Sin_ubicacion_no_se_guarda_la_HP(double? latitud, double? longitud)
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());

        var problema = await (await RegistrarAsync(operador, clienteId, await operador.CrearProductoAsync(10m),
            (decimal?)latitud, (decimal?)longitud)).ProblemaAsync(400, "validacion");

        Assert.True(problema.GetProperty("errors").TryGetProperty("latitud", out _));
    }

    [Fact]
    public async Task Un_tipo_de_venta_que_no_existe_responde_400()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());

        var problema = await (await RegistrarAsync(operador, clienteId, await operador.CrearProductoAsync(10m), tipoVenta: 5))
            .ProblemaAsync(400, "validacion");

        Assert.True(problema.GetProperty("errors").TryGetProperty("tipoVenta", out _));
    }

    [Fact]
    public async Task La_zona_de_la_HP_es_la_del_cliente_aunque_se_envie_otra()
    {
        using var operador = await api.OperadorAsync();
        var zonaDelCliente = await operador.CrearZonaAsync();
        var otraZona = await operador.CrearZonaAsync();
        var clienteId = await operador.CrearClienteAsync(zonaDelCliente);

        var hoja = await (await RegistrarAsync(operador, clienteId, await operador.CrearProductoAsync(10m), zonaId: otraZona))
            .LeerJsonAsync();

        Assert.Equal(zonaDelCliente, hoja.GetProperty("zona").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Solo_quien_registro_la_HP_la_confirma_aunque_otro_la_vea()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var hojaId = await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(zonaId), await operador.CrearProductoAsync(10m), 1);
        using var admin = await api.AdminAsync();
        var (_, rol) = await admin.CrearRolAsync(Permisos.Ventas.ConfirmarHp, Permisos.Seguridad.VerCarteraCompleta);
        var otro = await api.CrearUsuarioAsync(rol);
        using var clienteOtro = api.ClienteCon(otro.Token);

        await (await clienteOtro.PostAsync($"/api/hojas-pedido/{hojaId}/confirmar", null)).ProblemaAsync(400, "hp_de_otro_promotor");
        Assert.Equal(0, (await HojaDe(operador, hojaId)).GetProperty("estadoHP").GetInt32());
    }

    [Fact]
    public async Task La_busqueda_encuentra_la_HP_por_su_numero()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var hojaId = await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(zonaId),
            await operador.CrearProductoAsync(10m), 2);
        var hoja = await HojaDe(operador, hojaId);

        var encontradas = await IdsAsync(operador, $"?buscar={hoja.GetProperty("numero").GetString()}");

        Assert.Equal([hojaId], encontradas);
        var linea = Assert.Single(hoja.GetProperty("detalles").EnumerateArray());
        Assert.Equal(2m, linea.GetProperty("cantidad").GetDecimal());
        Assert.StartsWith("Producto P-", linea.GetProperty("descripcion").GetString());
    }
}
