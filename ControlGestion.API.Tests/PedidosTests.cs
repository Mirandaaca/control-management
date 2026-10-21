using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class PedidosTests(ApiDePrueba api)
{
    private static async Task<List<int>> IdsAsync(HttpClient cliente, string consulta = "") =>
        (await (await cliente.GetAsync($"/api/pedidos{consulta}")).LeerJsonAsync())
            .EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).ToList();

    private static async Task<JsonElement> PedidoDe(HttpClient cliente, int pedidoId) =>
        await (await cliente.GetAsync($"/api/pedidos/{pedidoId}")).LeerJsonAsync();

    private static async Task<HttpResponseMessage> RegistrarAsync(HttpClient cliente, int clienteId, object[] detalles,
        decimal descuento = 0)
    {
        var zonaId = (await (await cliente.GetAsync($"/api/clientes/{clienteId}")).LeerJsonAsync())
            .GetProperty("zona").GetProperty("id").GetInt32();
        return await cliente.PostAsJsonAsync("/api/pedidos", new
        {
            clienteId,
            zonaId,
            fechaEntregaEstimada = Ayudantes.MananaEnBolivia(),
            direccionEntrega = "Calle de entrega de prueba",
            descuento,
            detalles
        });
    }

    [Fact]
    public async Task Registrar_numera_el_pedido_y_calcula_el_subtotal_y_el_total()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var arroz = await operador.CrearProductoAsync(10m);
        var fideo = await operador.CrearProductoAsync(5.5m);

        var respuesta = await RegistrarAsync(operador, clienteId,
            [new { productoId = arroz, cantidad = 3, descuento = 1 }, new { productoId = fideo, cantidad = 2, descuento = 0 }],
            descuento: 3);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var pedido = await respuesta.LeerJsonAsync();
        Assert.Matches(new Regex($@"^PED-{DateTime.UtcNow.AddHours(-4).Year}-\d{{5}}$"), pedido.GetProperty("numero").GetString());
        // (10 − 1) × 3 + 5,50 × 2 = 38, menos 3 de descuento sobre el total.
        Assert.Equal(38m, pedido.GetProperty("subTotal").GetDecimal());
        Assert.Equal(35m, pedido.GetProperty("total").GetDecimal());
        Assert.Contains(pedido.GetProperty("id").GetInt32(), await IdsAsync(operador, $"?clienteId={clienteId}&estadoId=1"));
    }

    [Fact]
    public async Task El_catalogo_de_estados_trae_los_cinco_en_orden()
    {
        using var operador = await api.OperadorAsync();

        var estados = (await (await operador.GetAsync("/api/pedidos/estados")).LeerJsonAsync()).EnumerateArray()
            .Select(e => (e.GetProperty("id").GetInt32(), e.GetProperty("nombre").GetString())).ToList();

        Assert.Equal([(1, "Pendiente"), (2, "En Proceso"), (3, "En Ruta"), (4, "Entregado"), (5, "Anulado")], estados);
    }

    [Fact]
    public async Task Un_pedido_que_no_existe_responde_404()
    {
        using var operador = await api.OperadorAsync();

        await (await operador.GetAsync("/api/pedidos/999999")).ProblemaAsync(404, "no_encontrado");
    }

    [Fact]
    public async Task Un_producto_inactivo_no_entra_en_un_pedido()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var productoId = await operador.CrearProductoAsync(10m);
        (await operador.DeleteAsync($"/api/productos/{productoId}")).EnsureSuccessStatusCode();

        await (await RegistrarAsync(operador, clienteId, [new { productoId, cantidad = 1, descuento = 0 }]))
            .ProblemaAsync(400, "producto_no_disponible");
    }

    [Fact]
    public async Task La_secretaria_registra_pedidos_y_el_gerente_general_no()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var productoId = await operador.CrearProductoAsync(10m);
        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);
        using var gerente = api.ClienteCon((await api.CrearUsuarioAsync(Roles.GerenteGeneral)).Token);

        Assert.Equal(HttpStatusCode.Created,
            (await RegistrarAsync(secretaria, clienteId, [new { productoId, cantidad = 1, descuento = 0 }])).StatusCode);
        await (await gerente.PostAsJsonAsync("/api/pedidos", new { clienteId, detalles = new[] { new { productoId, cantidad = 1 } } }))
            .ProblemaAsync(403, "sin_permiso");
    }
}
