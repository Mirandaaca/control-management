using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class PedidosEntregaTests(ApiDePrueba api)
{
    private const int Contado = 0;
    private const int Credito = 1;

    private record Escenario(UsuarioDePrueba Promotor, int PedidoId, int ClienteId, int ProductoId);

    private static async Task<JsonElement> JsonDe(HttpClient cliente, string ruta) =>
        await (await cliente.GetAsync(ruta)).LeerJsonAsync();

    private static Task<HttpResponseMessage> EntregarAsync(HttpClient cliente, int pedidoId, int? tipoVenta = Credito,
        string recibidoPor = "Doña Rosa", decimal? latitud = -17.7801m, decimal? longitud = -63.1795m) =>
        cliente.PostAsJsonAsync($"/api/pedidos/{pedidoId}/entregar", new
        {
            recibidoPor, tipoVenta, latitud, longitud, precisionGPS = 8, observacion = "Dejado en el mostrador"
        });

    private static async Task AvanzarAsync(HttpClient cliente, int pedidoId, int veces)
    {
        for (var i = 0; i < veces; i++)
            (await cliente.PostAsJsonAsync($"/api/pedidos/{pedidoId}/avanzar", new { })).EnsureSuccessStatusCode();
    }

    private static async Task<decimal> StockDe(HttpClient cliente, int productoId) =>
        (await JsonDe(cliente, $"/api/productos/{productoId}")).GetProperty("stockActual").GetDecimal();

    private static async Task<List<JsonElement>> HojasDe(HttpClient cliente, int clienteId) =>
        (await JsonDe(cliente, $"/api/hojas-pedido?clienteId={clienteId}")).GetProperty("items").EnumerateArray().ToList();

    /// <summary>
    /// Un pedido de 3 unidades a 12,50 de un cliente de la cartera del promotor, asignado a él y en ruta (o en el estado
    /// que se pida: 0 avances es Pendiente).
    /// </summary>
    private async Task<Escenario> PedidoAsignadoAsync(HttpClient operador, decimal stock = 100, int avances = 2,
        decimal cantidad = 3)
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var productoId = await operador.CrearProductoAsync(12.5m, stockInicial: stock);
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id));
        var pedidoId = await operador.CrearPedidoAsync(clienteId, productoId, cantidad);
        (await operador.PostAsJsonAsync("/api/pedidos/asignar", new
        {
            pedidoIds = new[] { pedidoId },
            promotorId = promotor.Id,
            dia = HoraBolivia.Hoy(TimeProvider.System).AddDays(1).ToString("yyyy-MM-dd")
        })).EnsureSuccessStatusCode();
        await AvanzarAsync(operador, pedidoId, avances);
        return new Escenario(promotor, pedidoId, clienteId, productoId);
    }

    [Fact]
    public async Task Entregar_a_credito_crea_y_confirma_la_HP_descuenta_el_stock_y_crea_la_deuda()
    {
        using var operador = await api.OperadorAsync();
        var e = await PedidoAsignadoAsync(operador);
        using var promotor = api.ClienteCon(e.Promotor.Token);

        var respuesta = await EntregarAsync(promotor, e.PedidoId, recibidoPor: "  Doña Rosa  ");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var pedido = await respuesta.LeerJsonAsync();
        Assert.Equal(4, pedido.GetProperty("estado").GetProperty("id").GetInt32());
        Assert.Equal("Doña Rosa", pedido.GetProperty("recibidoPor").GetString());
        Assert.NotEqual(JsonValueKind.Null, pedido.GetProperty("fechaEntregaReal").ValueKind);
        Assert.Equal(-17.7801m, pedido.GetProperty("latitudEntrega").GetDecimal());
        Assert.Equal(3m, Assert.Single(pedido.GetProperty("detalles").EnumerateArray()).GetProperty("cantidadEntregada").GetDecimal());
        var entrada = pedido.GetProperty("historial").EnumerateArray().Last();
        Assert.Equal(4, entrada.GetProperty("estado").GetProperty("id").GetInt32());
        Assert.Equal(-63.1795m, entrada.GetProperty("longitud").GetDecimal());

        // La HP: del cliente, a nombre de quien entregó, confirmada, con el total del pedido y su deuda.
        var deLaEntrega = pedido.GetProperty("hojaPedido");
        Assert.Equal(1, deLaEntrega.GetProperty("estadoHP").GetInt32());
        Assert.Equal(Credito, deLaEntrega.GetProperty("tipoVenta").GetInt32());
        var hp = await JsonDe(operador, $"/api/hojas-pedido/{deLaEntrega.GetProperty("id").GetInt32()}");
        Assert.Equal(e.Promotor.Id, hp.GetProperty("promotor").GetProperty("id").GetString());
        Assert.Equal(e.ClienteId, hp.GetProperty("cliente").GetProperty("id").GetInt32());
        Assert.Equal(37.5m, hp.GetProperty("total").GetDecimal());
        Assert.Equal(-17.7801m, hp.GetProperty("latitud").GetDecimal());
        var cobranzaId = deLaEntrega.GetProperty("cobranza").GetProperty("id").GetInt32();
        Assert.Equal(cobranzaId, hp.GetProperty("cobranza").GetProperty("id").GetInt32());
        Assert.Equal(37.5m, (await JsonDe(operador, $"/api/cobranzas/{cobranzaId}")).GetProperty("montoTotal").GetDecimal());

        // El stock y el kardex, la visita y el saldo del cliente.
        Assert.Equal(97m, await StockDe(operador, e.ProductoId));
        var salida = (await JsonDe(operador, $"/api/inventario/{e.ProductoId}/movimientos")).GetProperty("items")[0];
        Assert.Equal(-3m, salida.GetProperty("variacion").GetDecimal());
        Assert.Equal(hp.GetProperty("numero").GetString(), salida.GetProperty("hojaPedido").GetProperty("numero").GetString());
        var cliente = await JsonDe(operador, $"/api/clientes/{e.ClienteId}");
        Assert.Equal("Activo", cliente.GetProperty("estado").GetProperty("nombre").GetString());
        Assert.Equal(37.5m, cliente.GetProperty("saldoPendiente").GetDecimal());
    }

    [Fact]
    public async Task Entregar_al_contado_no_crea_deuda()
    {
        using var operador = await api.OperadorAsync();
        var e = await PedidoAsignadoAsync(operador);
        using var promotor = api.ClienteCon(e.Promotor.Token);

        var pedido = await (await EntregarAsync(promotor, e.PedidoId, tipoVenta: Contado)).LeerJsonAsync();

        Assert.Equal(JsonValueKind.Null, pedido.GetProperty("hojaPedido").GetProperty("cobranza").ValueKind);
        Assert.Equal(97m, await StockDe(operador, e.ProductoId));
        Assert.Equal(0m, (await JsonDe(operador, $"/api/clientes/{e.ClienteId}")).GetProperty("saldoPendiente").GetDecimal());
    }

    [Fact]
    public async Task Sin_stock_no_se_registra_la_entrega_y_nada_cambia()
    {
        using var operador = await api.OperadorAsync();
        var e = await PedidoAsignadoAsync(operador, stock: 2, cantidad: 5);
        using var promotor = api.ClienteCon(e.Promotor.Token);

        await (await EntregarAsync(promotor, e.PedidoId)).ProblemaAsync(400, "stock_insuficiente");

        var pedido = await JsonDe(operador, $"/api/pedidos/{e.PedidoId}");
        Assert.Equal(3, pedido.GetProperty("estado").GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Null, pedido.GetProperty("hojaPedido").ValueKind);
        Assert.Empty(await HojasDe(operador, e.ClienteId));
        Assert.Equal(2m, await StockDe(operador, e.ProductoId));
    }

    [Fact]
    public async Task Sin_stock_el_error_cae_en_la_linea_del_producto_que_no_alcanza()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var conStock = await operador.CrearProductoAsync(12.5m, stockInicial: 100);
        var sinStock = await operador.CrearProductoAsync(20m, stockInicial: 1);
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id));
        var registrado = await operador.PostAsJsonAsync("/api/pedidos", new
        {
            clienteId,
            fechaEntregaEstimada = Ayudantes.MananaEnBolivia(),
            direccionEntrega = "Calle de entrega de prueba",
            detalles = new[] { new { productoId = conStock, cantidad = 2m }, new { productoId = sinStock, cantidad = 3m } }
        });
        var pedidoId = (await registrado.LeerJsonAsync()).GetProperty("id").GetInt32();
        (await operador.PostAsJsonAsync("/api/pedidos/asignar", new
        {
            pedidoIds = new[] { pedidoId },
            promotorId = promotor.Id,
            dia = HoraBolivia.Hoy(TimeProvider.System).AddDays(1).ToString("yyyy-MM-dd")
        })).EnsureSuccessStatusCode();
        await AvanzarAsync(operador, pedidoId, 2);

        var problema = await (await EntregarAsync(api.ClienteCon(promotor.Token), pedidoId)).ProblemaAsync(400, "stock_insuficiente");

        // La app pone el error en la línea i del detalle del pedido, que va en el orden en que se guardaron.
        var errores = problema.GetProperty("errors").EnumerateObject().Select(e => e.Name).ToList();
        Assert.Equal(["detalles[1].cantidad"], errores);
    }

    [Fact]
    public async Task Solo_entrega_el_promotor_asignado()
    {
        using var operador = await api.OperadorAsync();
        var promotorDelCliente = await api.CrearUsuarioAsync(Roles.Promotor);
        var asignado = await api.CrearUsuarioAsync(Roles.Promotor);
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotorDelCliente.Id));
        var pedidoId = await operador.CrearPedidoAsync(clienteId, await operador.CrearProductoAsync(10m), 1);
        await AvanzarAsync(operador, pedidoId, 2);

        // En ruta pero sin asignar: nadie lo puede entregar todavía.
        await (await EntregarAsync(operador, pedidoId)).ProblemaAsync(400, "pedido_sin_asignar");

        (await operador.PostAsJsonAsync("/api/pedidos/asignar", new
        {
            pedidoIds = new[] { pedidoId }, promotorId = asignado.Id,
            dia = HoraBolivia.Hoy(TimeProvider.System).AddDays(1).ToString("yyyy-MM-dd")
        })).EnsureSuccessStatusCode();

        // El promotor del cliente lo ve por su cartera, pero no es quien lo lleva.
        await (await EntregarAsync(api.ClienteCon(promotorDelCliente.Token), pedidoId)).ProblemaAsync(400, "pedido_de_otro_promotor");
        await (await EntregarAsync(operador, pedidoId)).ProblemaAsync(400, "pedido_de_otro_promotor");
        Assert.Equal(HttpStatusCode.OK, (await EntregarAsync(api.ClienteCon(asignado.Token), pedidoId)).StatusCode);
    }

    [Fact]
    public async Task Solo_se_entrega_un_pedido_en_ruta_y_una_sola_vez()
    {
        using var operador = await api.OperadorAsync();
        var e = await PedidoAsignadoAsync(operador, avances: 1);
        using var promotor = api.ClienteCon(e.Promotor.Token);

        await (await EntregarAsync(promotor, e.PedidoId)).ProblemaAsync(400, "pedido_no_esta_en_ruta");
        await AvanzarAsync(operador, e.PedidoId, 1);
        (await EntregarAsync(promotor, e.PedidoId)).EnsureSuccessStatusCode();

        await (await EntregarAsync(promotor, e.PedidoId)).ProblemaAsync(400, "pedido_en_estado_final");
        await (await operador.PostAsJsonAsync($"/api/pedidos/{e.PedidoId}/anular", new { motivo = "Tarde" }))
            .ProblemaAsync(400, "pedido_en_estado_final");
        Assert.Single(await HojasDe(operador, e.ClienteId));
    }

    [Fact]
    public async Task Quien_recibio_el_tipo_de_venta_y_la_ubicacion_son_obligatorios()
    {
        using var operador = await api.OperadorAsync();
        var e = await PedidoAsignadoAsync(operador);
        using var promotor = api.ClienteCon(e.Promotor.Token);

        var problema = await (await EntregarAsync(promotor, e.PedidoId, tipoVenta: null, recibidoPor: " ", latitud: null))
            .ProblemaAsync(400, "validacion");
        var errores = problema.GetProperty("errors");
        foreach (var campo in new[] { "recibidoPor", "tipoVenta", "latitud" })
            Assert.True(errores.TryGetProperty(campo, out _), $"Falta errors.{campo}.");

        // 0,0 es lo que llega cuando nadie tomó la ubicación.
        var sinUbicacion = await (await EntregarAsync(promotor, e.PedidoId, latitud: 0, longitud: 0)).ProblemaAsync(400, "validacion");
        Assert.True(sinUbicacion.GetProperty("errors").TryGetProperty("latitud", out _));
    }

    [Fact]
    public async Task Un_cliente_que_paso_a_inactivo_no_recibe_la_entrega()
    {
        using var operador = await api.OperadorAsync();
        var e = await PedidoAsignadoAsync(operador);
        (await operador.PutAsJsonAsync($"/api/clientes/{e.ClienteId}/estado", new { estadoClienteId = 4 })).EnsureSuccessStatusCode();

        await (await EntregarAsync(api.ClienteCon(e.Promotor.Token), e.PedidoId)).ProblemaAsync(400, "cliente_inactivo");
    }

    [Fact]
    public async Task La_secretaria_no_registra_entregas_y_el_endpoint_viejo_de_estado_ya_no_existe()
    {
        using var operador = await api.OperadorAsync();
        var e = await PedidoAsignadoAsync(operador);
        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);

        await (await EntregarAsync(secretaria, e.PedidoId)).ProblemaAsync(403, "sin_permiso");
        await (await operador.PostAsJsonAsync($"/api/pedidos/{e.PedidoId}/estado", new { nuevoEstadoId = 4 })).ProblemaAsync(404);
    }

    [Fact]
    public async Task Anular_la_HP_de_una_entrega_devuelve_el_stock_y_el_pedido_sigue_entregado()
    {
        using var operador = await api.OperadorAsync();
        var e = await PedidoAsignadoAsync(operador);
        var hojaId = (await (await EntregarAsync(api.ClienteCon(e.Promotor.Token), e.PedidoId, tipoVenta: Contado)).LeerJsonAsync())
            .GetProperty("hojaPedido").GetProperty("id").GetInt32();

        (await operador.PostAsJsonAsync($"/api/hojas-pedido/{hojaId}/anular", new { motivo = "Devolvieron la mercadería" }))
            .EnsureSuccessStatusCode();

        var pedido = await JsonDe(operador, $"/api/pedidos/{e.PedidoId}");
        Assert.Equal(4, pedido.GetProperty("estado").GetProperty("id").GetInt32());
        Assert.Equal(2, pedido.GetProperty("hojaPedido").GetProperty("estadoHP").GetInt32());
        Assert.Equal(100m, await StockDe(operador, e.ProductoId));
    }

    [Fact]
    public async Task Dos_entregas_a_la_vez_crean_una_sola_HP_y_descuentan_el_stock_una_vez()
    {
        using var operador = await api.OperadorAsync();
        var e = await PedidoAsignadoAsync(operador);
        using var promotor = api.ClienteCon(e.Promotor.Token);

        var respuestas = await Ayudantes.EnviarALaVezAsync(2, _ => EntregarAsync(promotor, e.PedidoId));

        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.OK);
        // La otra choca con el token (409) o llega cuando el pedido ya está entregado (400): nunca un 500.
        var otra = Assert.Single(respuestas, r => r.StatusCode != HttpStatusCode.OK);
        Assert.Contains(otra.StatusCode, new[] { HttpStatusCode.Conflict, HttpStatusCode.BadRequest });
        Assert.Single(await HojasDe(operador, e.ClienteId));
        Assert.Equal(97m, await StockDe(operador, e.ProductoId));
    }
}
