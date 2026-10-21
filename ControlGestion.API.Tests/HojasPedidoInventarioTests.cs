using System.Text.Json;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class HojasPedidoInventarioTests(ApiDePrueba api)
{
    private static async Task<JsonElement> InventarioDe(HttpClient cliente, int productoId) =>
        (await (await cliente.GetAsync("/api/inventario")).LeerJsonAsync())
            .EnumerateArray().Single(i => i.GetProperty("productoId").GetInt32() == productoId);

    [Fact]
    public async Task Un_producto_nuevo_aparece_en_el_inventario_con_stock_cero()
    {
        using var operador = await api.OperadorAsync();
        var productoId = await operador.CrearProductoAsync(5m, stockInicial: 0);

        Assert.Equal(0m, (await InventarioDe(operador, productoId)).GetProperty("stockActual").GetDecimal());
    }

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
        Assert.Equal(97m, (await InventarioDe(operador, productoId)).GetProperty("stockActual").GetDecimal());
    }
}
