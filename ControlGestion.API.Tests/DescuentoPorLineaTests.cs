using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

/// <summary>El descuento por unidad no supera el precio del producto, en el pedido (1.1) y en la HP (2.1).</summary>
[Collection(ColeccionApi.Nombre)]
public class DescuentoPorLineaTests(ApiDePrueba api)
{
    /// <summary>Dos líneas del producto de 10: la primera sin descuento y la segunda con el descuento indicado.</summary>
    private static object[] Lineas(int productoId, decimal descuentoDeLaSegunda) =>
    [
        new { productoId, cantidad = 2, descuento = 0m },
        new { productoId, cantidad = 1, descuento = descuentoDeLaSegunda }
    ];

    private static async Task SoloLaSegundaLineaTieneErrorAsync(HttpResponseMessage respuesta)
    {
        var errores = (await respuesta.ProblemaAsync(400, "validacion")).GetProperty("errors");
        Assert.Equal(["detalles[1].descuento"], errores.EnumerateObject().Select(e => e.Name));
        Assert.Contains("no puede superar el precio", errores.GetProperty("detalles[1].descuento")[0].GetString());
    }

    private static Task<HttpResponseMessage> RegistrarPedidoAsync(HttpClient cliente, int clienteId, object[] detalles) =>
        cliente.PostAsJsonAsync("/api/pedidos", new
        {
            clienteId,
            fechaEntregaEstimada = Ayudantes.MananaEnBolivia(),
            direccionEntrega = "Calle de entrega de prueba",
            detalles
        });

    private static Task<HttpResponseMessage> RegistrarHojaAsync(HttpClient cliente, int clienteId, object[] detalles) =>
        cliente.PostAsJsonAsync("/api/hojas-pedido", new { clienteId, latitud = -17.7833m, longitud = -63.1821m, detalles });

    [Fact]
    public async Task Registrar_un_pedido_con_un_descuento_mayor_que_el_precio_marca_esa_linea()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var productoId = await operador.CrearProductoAsync(10m);

        await SoloLaSegundaLineaTieneErrorAsync(await RegistrarPedidoAsync(operador, clienteId, Lineas(productoId, 10.01m)));
    }

    [Fact]
    public async Task Modificar_un_pedido_con_un_descuento_mayor_que_el_precio_marca_esa_linea()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var productoId = await operador.CrearProductoAsync(10m);
        var pedidoId = await operador.CrearPedidoAsync(clienteId, productoId, 1);

        await SoloLaSegundaLineaTieneErrorAsync(await operador.PutAsJsonAsync($"/api/pedidos/{pedidoId}", new
        {
            fechaEntregaEstimada = Ayudantes.MananaEnBolivia(),
            direccionEntrega = "Calle de entrega de prueba",
            detalles = Lineas(productoId, 15m)
        }));
    }

    [Fact]
    public async Task Registrar_una_HP_con_un_descuento_mayor_que_el_precio_marca_esa_linea()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var productoId = await operador.CrearProductoAsync(10m);

        await SoloLaSegundaLineaTieneErrorAsync(await RegistrarHojaAsync(operador, clienteId, Lineas(productoId, 12m)));
    }

    [Fact]
    public async Task Un_descuento_igual_al_precio_se_acepta_como_un_producto_de_regalo()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var productoId = await operador.CrearProductoAsync(10m);

        var pedido = await (await RegistrarPedidoAsync(operador, clienteId, Lineas(productoId, 10m))).LeerJsonAsync();
        var hoja = await (await RegistrarHojaAsync(operador, clienteId, Lineas(productoId, 10m))).LeerJsonAsync();

        foreach (var venta in new[] { pedido, hoja })
        {
            Assert.Equal([0m, 20m], venta.GetProperty("detalles").EnumerateArray().Select(SubtotalDe).Order());
            Assert.Equal(20m, venta.GetProperty("total").GetDecimal());
        }
    }

    private static decimal SubtotalDe(JsonElement linea) => linea.GetProperty("subtotal").GetDecimal();
}
