using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class HojasPedidoInventarioTests(ApiDePrueba api)
{
    private static async Task<decimal> StockDe(HttpClient cliente, int productoId) =>
        (await (await cliente.GetAsync($"/api/productos/{productoId}")).LeerJsonAsync()).GetProperty("stockActual").GetDecimal();

    private static async Task<List<JsonElement>> KardexDe(HttpClient cliente, int productoId) =>
        (await (await cliente.GetAsync($"/api/inventario/{productoId}/movimientos")).LeerJsonAsync()).GetProperty("items").EnumerateArray().ToList();

    private static async Task<JsonElement> HojaDe(HttpClient cliente, int hojaId) =>
        await (await cliente.GetAsync($"/api/hojas-pedido/{hojaId}")).LeerJsonAsync();

    private static async Task<List<JsonElement>> CobranzasDe(HttpClient cliente, int clienteId) =>
        (await (await cliente.GetAsync($"/api/cobranzas?clienteId={clienteId}")).LeerJsonAsync()).EnumerateArray().ToList();

    private static Task<HttpResponseMessage> AnularAsync(HttpClient cliente, int hojaId, string motivo = "Venta registrada por error") =>
        cliente.PostAsJsonAsync($"/api/hojas-pedido/{hojaId}/anular", new { motivo });

    [Fact]
    public async Task Confirmar_una_HP_a_credito_crea_la_cobranza_y_descuenta_el_stock()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var productoId = await operador.CrearProductoAsync(12.5m, stockInicial: 100);
        var clienteId = await operador.CrearClienteAsync(zonaId);

        var cobranzaId = await operador.CrearCobranzaAsync(clienteId, zonaId, productoId, cantidad: 3);

        var cobranza = await (await operador.GetAsync($"/api/cobranzas/{cobranzaId}")).LeerJsonAsync();
        Assert.Equal(37.5m, cobranza.GetProperty("montoTotal").GetDecimal());
        Assert.Equal(0m, cobranza.GetProperty("montoPagado").GetDecimal());
        Assert.Equal(97m, await StockDe(operador, productoId));
    }

    [Fact]
    public async Task Una_HP_al_contado_confirmada_descuenta_el_stock_sin_deuda_y_cuenta_como_visita()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var productoId = await operador.CrearProductoAsync(10m, stockInicial: 100);
        var clienteId = await operador.CrearClienteAsync(zonaId);
        var hojaId = await operador.CrearHojaPedidoAsync(clienteId, zonaId, productoId, 4, credito: false);

        (await operador.PostAsync($"/api/hojas-pedido/{hojaId}/confirmar", null)).EnsureSuccessStatusCode();

        Assert.Equal(96m, await StockDe(operador, productoId));
        Assert.Empty(await CobranzasDe(operador, clienteId));
        var cliente = await (await operador.GetAsync($"/api/clientes/{clienteId}")).LeerJsonAsync();
        Assert.Equal("Activo", cliente.GetProperty("estado").GetProperty("nombre").GetString());
        Assert.NotEqual(JsonValueKind.Null, cliente.GetProperty("ultimaVisita").ValueKind);
        Assert.Equal(0m, cliente.GetProperty("saldoPendiente").GetDecimal());
    }

    [Fact]
    public async Task La_cobranza_de_una_HP_a_credito_vence_a_los_30_dias()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var cobranzaId = await operador.CrearCobranzaAsync(await operador.CrearClienteAsync(zonaId), zonaId,
            await operador.CrearProductoAsync(10m), 1);

        var cobranza = await (await operador.GetAsync($"/api/cobranzas/{cobranzaId}")).LeerJsonAsync();

        var plazo = cobranza.GetProperty("fechaVencimiento").GetDateTime() - cobranza.GetProperty("fechaEmision").GetDateTime();
        Assert.Equal(30, plazo.TotalDays, precision: 3);
    }

    [Fact]
    public async Task Confirmar_dos_veces_responde_hp_no_es_borrador()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var productoId = await operador.CrearProductoAsync(10m);
        var hojaId = await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(zonaId), zonaId, productoId, 1);
        (await operador.PostAsync($"/api/hojas-pedido/{hojaId}/confirmar", null)).EnsureSuccessStatusCode();

        await (await operador.PostAsync($"/api/hojas-pedido/{hojaId}/confirmar", null)).ProblemaAsync(400, "hp_no_es_borrador");
        Assert.Equal(99m, await StockDe(operador, productoId));
    }

    [Fact]
    public async Task Anular_un_borrador_no_mueve_el_stock()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var productoId = await operador.CrearProductoAsync(10m, stockInicial: 50);
        var hojaId = await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(zonaId), zonaId, productoId, 5);

        (await AnularAsync(operador, hojaId)).EnsureSuccessStatusCode();

        var hoja = await HojaDe(operador, hojaId);
        Assert.Equal(2, hoja.GetProperty("estadoHP").GetInt32());
        Assert.Equal(50m, await StockDe(operador, productoId));
        Assert.Single(await KardexDe(operador, productoId));
    }

    [Fact]
    public async Task Anular_dos_veces_responde_hp_ya_anulada()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var hojaId = await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(zonaId), zonaId,
            await operador.CrearProductoAsync(10m), 1);
        (await AnularAsync(operador, hojaId)).EnsureSuccessStatusCode();

        await (await AnularAsync(operador, hojaId)).ProblemaAsync(400, "hp_ya_anulada");
    }

    [Fact]
    public async Task Anular_sin_motivo_responde_400()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var hojaId = await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(zonaId), zonaId,
            await operador.CrearProductoAsync(10m), 1);

        var problema = await (await AnularAsync(operador, hojaId, motivo: "")).ProblemaAsync(400, "validacion");

        Assert.True(problema.GetProperty("errors").TryGetProperty("motivo", out _));
        Assert.Equal(0, (await HojaDe(operador, hojaId)).GetProperty("estadoHP").GetInt32());
    }

    [Fact]
    public async Task Un_ingreso_suma_al_stock_y_queda_en_el_kardex()
    {
        using var operador = await api.OperadorAsync();
        var productoId = await operador.CrearProductoAsync(10m, stockInicial: 0);

        (await operador.RegistrarIngresoAsync(productoId, 25m)).EnsureSuccessStatusCode();

        Assert.Equal(25m, await StockDe(operador, productoId));
        var movimiento = Assert.Single(await KardexDe(operador, productoId));
        Assert.Equal(25m, movimiento.GetProperty("cantidad").GetDecimal());
        Assert.Equal(0m, movimiento.GetProperty("stockAnterior").GetDecimal());
        Assert.Equal(25m, movimiento.GetProperty("stockResultante").GetDecimal());
    }
}
