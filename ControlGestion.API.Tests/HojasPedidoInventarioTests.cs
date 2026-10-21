using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Data;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    private static async Task<JsonElement> ClienteDe(HttpClient cliente, int clienteId) =>
        await (await cliente.GetAsync($"/api/clientes/{clienteId}")).LeerJsonAsync();

    private static Task<HttpResponseMessage> ConfirmarAsync(HttpClient cliente, int hojaId) =>
        cliente.PostAsync($"/api/hojas-pedido/{hojaId}/confirmar", null);

    private static Task<HttpResponseMessage> AnularAsync(HttpClient cliente, int hojaId, string motivo = "Venta registrada por error") =>
        cliente.PostAsJsonAsync($"/api/hojas-pedido/{hojaId}/anular", new { motivo });

    [Fact]
    public async Task Confirmar_una_HP_a_credito_crea_la_cobranza_y_descuenta_el_stock()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var productoId = await operador.CrearProductoAsync(12.5m, stockInicial: 100);
        var clienteId = await operador.CrearClienteAsync(zonaId);

        var cobranzaId = await operador.CrearCobranzaAsync(clienteId, productoId, cantidad: 3);

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
        var hojaId = await operador.CrearHojaPedidoAsync(clienteId, productoId, 4, credito: false);

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
        var cobranzaId = await operador.CrearCobranzaAsync(await operador.CrearClienteAsync(zonaId),
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
        var hojaId = await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(zonaId), productoId, 1);
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
        var hojaId = await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(zonaId), productoId, 5);

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
        var hojaId = await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(zonaId),
            await operador.CrearProductoAsync(10m), 1);
        (await AnularAsync(operador, hojaId)).EnsureSuccessStatusCode();

        await (await AnularAsync(operador, hojaId)).ProblemaAsync(400, "hp_ya_anulada");
    }

    [Fact]
    public async Task Anular_sin_motivo_responde_400()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var hojaId = await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(zonaId),
            await operador.CrearProductoAsync(10m), 1);

        var problema = await (await AnularAsync(operador, hojaId, motivo: "")).ProblemaAsync(400, "validacion");

        Assert.True(problema.GetProperty("errors").TryGetProperty("motivo", out _));
        Assert.Equal(0, (await HojaDe(operador, hojaId)).GetProperty("estadoHP").GetInt32());
    }

    [Fact]
    public async Task Sin_stock_suficiente_no_se_confirma_y_no_cambia_nada()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var productoId = await operador.CrearProductoAsync(10m, stockInicial: 2);
        var clienteId = await operador.CrearClienteAsync(zonaId);
        var hojaId = await operador.CrearHojaPedidoAsync(clienteId, productoId, 3);

        var problema = await (await ConfirmarAsync(operador, hojaId)).ProblemaAsync(400, "stock_insuficiente");

        Assert.Contains("hay 2", problema.GetProperty("detail").GetString());
        Assert.Equal(2m, await StockDe(operador, productoId));
        Assert.Equal(0, (await HojaDe(operador, hojaId)).GetProperty("estadoHP").GetInt32());
        Assert.Empty(await CobranzasDe(operador, clienteId));
        var cliente = await ClienteDe(operador, clienteId);
        Assert.Equal(JsonValueKind.Null, cliente.GetProperty("ultimaVisita").ValueKind);
        Assert.Equal(0m, cliente.GetProperty("saldoPendiente").GetDecimal());
    }

    [Fact]
    public async Task Dos_lineas_del_mismo_producto_se_comparan_con_el_stock_sumadas()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var productoId = await operador.CrearProductoAsync(10m, stockInicial: 5);
        var respuesta = await operador.PostAsJsonAsync("/api/hojas-pedido", new
        {
            clienteId = await operador.CrearClienteAsync(zonaId),
            latitud = -17.78m, longitud = -63.18m, tipoVenta = 0,
            detalles = new[] { new { productoId, cantidad = 3 }, new { productoId, cantidad = 3 } }
        });
        var hojaId = (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt32();

        await (await ConfirmarAsync(operador, hojaId)).ProblemaAsync(400, "stock_insuficiente");
        Assert.Equal(5m, await StockDe(operador, productoId));
    }

    [Fact]
    public async Task Confirmar_deja_la_salida_en_el_kardex_con_su_HP()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var productoId = await operador.CrearProductoAsync(10m, stockInicial: 10);
        var hojaId = await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(zonaId), productoId, 3, credito: false);

        (await ConfirmarAsync(operador, hojaId)).EnsureSuccessStatusCode();

        var salida = (await KardexDe(operador, productoId))[0];
        Assert.Equal(1, salida.GetProperty("tipoMovimiento").GetInt32());
        Assert.Equal(-3m, salida.GetProperty("variacion").GetDecimal());
        Assert.Equal(hojaId, salida.GetProperty("hojaPedido").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Confirmar_a_credito_recalcula_el_saldo_desde_las_cobranzas_abiertas()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var clienteId = await operador.CrearClienteAsync(zonaId);
        var primera = await operador.CrearCobranzaAsync(clienteId, await operador.CrearProductoAsync(100m), 1);
        (await operador.PostAsJsonAsync($"/api/cobranzas/{primera}/pagos", new { monto = 40, metodoPago = 0 })).EnsureSuccessStatusCode();
        // Un saldo guardado mal: antes, confirmar le sumaba el total encima, y el error seguía para siempre.
        using (var scope = api.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Clientes
                .Where(c => c.Id == clienteId).ExecuteUpdateAsync(s => s.SetProperty(c => c.SaldoPendiente, 999m));

        await operador.CrearCobranzaAsync(clienteId, await operador.CrearProductoAsync(25m), 1);

        Assert.Equal(85m, (await ClienteDe(operador, clienteId)).GetProperty("saldoPendiente").GetDecimal());
    }

    [Fact]
    public async Task Dos_HP_a_la_vez_que_juntas_superan_el_stock_nunca_lo_dejan_negativo()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var productoId = await operador.CrearProductoAsync(10m, stockInicial: 10);
        var hojas = new[]
        {
            await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(zonaId), productoId, 6, credito: false),
            await operador.CrearHojaPedidoAsync(await operador.CrearClienteAsync(zonaId), productoId, 6, credito: false)
        };

        var respuestas = await Ayudantes.EnviarALaVezAsync(2, i => ConfirmarAsync(operador, hojas[i]));

        // La segunda choca con la primera (409) o llega después y ya no hay stock (400).
        Assert.Single(respuestas, r => (int)r.StatusCode == 200);
        Assert.Single(respuestas, r => (int)r.StatusCode is 400 or 409);
        Assert.Equal(4m, await StockDe(operador, productoId));
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
