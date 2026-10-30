using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class PedidosTests(ApiDePrueba api)
{
    private static async Task<List<int>> IdsAsync(HttpClient cliente, string consulta = "") =>
        (await (await cliente.GetAsync($"/api/pedidos{consulta}")).LeerJsonAsync())
            .GetProperty("items").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).ToList();

    private static async Task<JsonElement> PedidoDe(HttpClient cliente, int pedidoId) =>
        await (await cliente.GetAsync($"/api/pedidos/{pedidoId}")).LeerJsonAsync();

    private static Task<HttpResponseMessage> RegistrarAsync(HttpClient cliente, int clienteId, object[] detalles,
        decimal descuento = 0, string? fechaEntregaEstimada = null, int? zonaId = null) =>
        cliente.PostAsJsonAsync("/api/pedidos", new
        {
            clienteId,
            zonaId,
            fechaEntregaEstimada = fechaEntregaEstimada ?? Ayudantes.MananaEnBolivia(),
            direccionEntrega = "Calle de entrega de prueba",
            descuento,
            detalles
        });

    private static object[] Linea(int productoId, decimal cantidad = 1, decimal descuento = 0) =>
        [new { productoId, cantidad, descuento }];

    /// <summary>Dos promotores, cada uno con una zona, un cliente y un pedido de ese cliente.</summary>
    private async Task<(UsuarioDePrueba PromotorA, int PedidoDeA, int PedidoDeB)> DosCarterasAsync(HttpClient operador)
    {
        var productoId = await operador.CrearProductoAsync(10m);
        var promotorA = await api.CrearUsuarioAsync(Roles.Promotor);
        var promotorB = await api.CrearUsuarioAsync(Roles.Promotor);
        var pedidoDeA = await operador.CrearPedidoAsync(
            await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotorA.Id)), productoId, 1);
        var pedidoDeB = await operador.CrearPedidoAsync(
            await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotorB.Id)), productoId, 1);
        return (promotorA, pedidoDeA, pedidoDeB);
    }

    [Fact]
    public async Task Un_promotor_solo_ve_los_pedidos_de_los_clientes_de_su_cartera()
    {
        using var operador = await api.OperadorAsync();
        var (promotorA, pedidoDeA, pedidoDeB) = await DosCarterasAsync(operador);
        using var clienteA = api.ClienteCon(promotorA.Token);

        Assert.Equal([pedidoDeA], await IdsAsync(clienteA));
        Assert.Equal(pedidoDeA, (await PedidoDe(clienteA, pedidoDeA)).GetProperty("id").GetInt32());
        await (await clienteA.GetAsync($"/api/pedidos/{pedidoDeB}")).ProblemaAsync(404, "no_encontrado");
        await (await clienteA.GetAsync($"/api/pedidos/{pedidoDeB}/historial")).ProblemaAsync(404, "no_encontrado");
    }

    [Fact]
    public async Task La_busqueda_encuentra_el_pedido_por_su_numero_y_por_el_cliente()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var pedidoId = await operador.CrearPedidoAsync(clienteId, await operador.CrearProductoAsync(10m), 2);
        var pedido = await PedidoDe(operador, pedidoId);
        var nombreDelCliente = pedido.GetProperty("cliente").GetProperty("nombreCompleto").GetString();

        Assert.Equal([pedidoId], await IdsAsync(operador, $"?buscar={pedido.GetProperty("numero").GetString()}"));
        Assert.Equal([pedidoId], await IdsAsync(operador, $"?buscar={Uri.EscapeDataString(nombreDelCliente!)}"));
        var linea = Assert.Single(pedido.GetProperty("detalles").EnumerateArray());
        Assert.Equal(2m, linea.GetProperty("cantidad").GetDecimal());
        Assert.StartsWith("Producto P-", linea.GetProperty("descripcion").GetString());
    }

    [Fact]
    public async Task La_lista_viene_paginada()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var productoId = await operador.CrearProductoAsync(10m);
        for (var i = 0; i < 3; i++) await operador.CrearPedidoAsync(clienteId, productoId, 1);

        var pagina = await (await operador.GetAsync($"/api/pedidos?clienteId={clienteId}&tamano=2&pagina=2")).LeerJsonAsync();

        Assert.Equal(3, pagina.GetProperty("total").GetInt32());
        Assert.Equal(2, pagina.GetProperty("totalPaginas").GetInt32());
        Assert.Single(pagina.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Registrar_numera_el_pedido_y_calcula_el_subtotal_y_el_total()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var arroz = await operador.CrearProductoAsync(10m);
        var fideo = await operador.CrearProductoAsync(5.5m);

        var respuesta = await RegistrarAsync(operador, clienteId,
            [new { productoId = arroz, cantidad = 3m, descuento = 1m }, new { productoId = fideo, cantidad = 2m, descuento = 0m }],
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
    public async Task El_catalogo_de_estados_trae_los_cuatro_en_orden_sin_renumerar()
    {
        using var operador = await api.OperadorAsync();

        var estados = (await (await operador.GetAsync("/api/pedidos/estados")).LeerJsonAsync()).EnumerateArray()
            .Select(e => (e.GetProperty("id").GetInt32(), e.GetProperty("nombre").GetString())).ToList();

        Assert.Equal([(1, "Pendiente"), (3, "En ruta"), (4, "Entregado"), (5, "Anulado")], estados);
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

        await (await RegistrarAsync(operador, clienteId, Linea(productoId)))
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
            (await RegistrarAsync(secretaria, clienteId, Linea(productoId))).StatusCode);
        await (await gerente.PostAsJsonAsync("/api/pedidos", new { clienteId, detalles = new[] { new { productoId, cantidad = 1 } } }))
            .ProblemaAsync(403, "sin_permiso");
    }

    [Fact]
    public async Task El_pedido_queda_con_el_promotor_del_cliente_y_a_nombre_de_quien_lo_registro()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id));
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        using var clienteSecretaria = api.ClienteCon(secretaria.Token);

        var pedido = await (await RegistrarAsync(clienteSecretaria, clienteId, Linea(await operador.CrearProductoAsync(10m))))
            .LeerJsonAsync();

        Assert.Equal(promotor.Id, pedido.GetProperty("promotor").GetProperty("id").GetString());
        Assert.Equal(secretaria.Id, pedido.GetProperty("registradoPor").GetProperty("id").GetString());
        var entrada = Assert.Single(pedido.GetProperty("historial").EnumerateArray());
        Assert.Equal("Pedido registrado", entrada.GetProperty("observacion").GetString());
        Assert.Equal(secretaria.Id, entrada.GetProperty("usuario").GetProperty("id").GetString());
        // El pedido es de la cartera del promotor: él lo ve aunque lo haya registrado la secretaria.
        using var clientePromotor = api.ClienteCon(promotor.Token);
        Assert.Contains(pedido.GetProperty("id").GetInt32(), await IdsAsync(clientePromotor));
    }

    [Fact]
    public async Task La_zona_del_pedido_es_la_del_cliente_aunque_se_envie_otra()
    {
        using var operador = await api.OperadorAsync();
        var zonaDelCliente = await operador.CrearZonaAsync();
        var otraZona = await operador.CrearZonaAsync();
        var clienteId = await operador.CrearClienteAsync(zonaDelCliente);

        var pedido = await (await RegistrarAsync(operador, clienteId, Linea(await operador.CrearProductoAsync(10m)), zonaId: otraZona))
            .LeerJsonAsync();

        Assert.Equal(zonaDelCliente, pedido.GetProperty("zona").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Un_cliente_sin_promotor_deja_el_pedido_sin_promotor()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        // Una zona siempre tiene promotor (7.2): un cliente sin promotor solo queda de los datos viejos.
        using (var scope = api.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Clientes.Where(c => c.Id == clienteId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.PromotorId, (string?)null));

        var pedido = await (await RegistrarAsync(operador, clienteId, Linea(await operador.CrearProductoAsync(10m)))).LeerJsonAsync();

        Assert.Equal(JsonValueKind.Null, pedido.GetProperty("promotor").ValueKind);
    }

    [Fact]
    public async Task Un_promotor_no_registra_pedidos_para_un_cliente_fuera_de_su_cartera_ni_para_uno_que_no_existe()
    {
        using var operador = await api.OperadorAsync();
        var productoId = await operador.CrearProductoAsync(10m);
        var promotorA = await api.CrearUsuarioAsync(Roles.Promotor);
        var promotorB = await api.CrearUsuarioAsync(Roles.Promotor);
        var clienteDeB = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotorB.Id));
        using var clienteA = api.ClienteCon(promotorA.Token);

        // Responde igual que si el cliente no existiera, y un id inexistente ya no llega a la base (T6.13).
        await (await RegistrarAsync(clienteA, clienteDeB, Linea(productoId))).ProblemaAsync(400, "cliente_no_existe");
        await (await RegistrarAsync(operador, 999999, Linea(productoId))).ProblemaAsync(400, "cliente_no_existe");
    }

    [Fact]
    public async Task Un_cliente_inactivo_no_recibe_pedidos()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        (await operador.PutAsJsonAsync($"/api/clientes/{clienteId}/estado", new { estadoClienteId = 4 })).EnsureSuccessStatusCode();

        await (await RegistrarAsync(operador, clienteId, Linea(await operador.CrearProductoAsync(10m)))).ProblemaAsync(400, "cliente_inactivo");
    }

    [Fact]
    public async Task Un_total_de_cero_o_menos_no_se_guarda()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());

        // T6.10: un descuento de 1000 sobre 32 dejaba el total en −968.
        var problema = await (await RegistrarAsync(operador, clienteId, Linea(await operador.CrearProductoAsync(8m), cantidad: 4),
            descuento: 1000)).ProblemaAsync(400, "total_invalido");
        // En Bs y con coma, como los demás errores (aviso de la tarea 13b).
        Assert.Equal("El total del pedido debe ser mayor a cero: el subtotal es Bs 32,00 y el descuento, Bs 1.000,00.",
            problema.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Los_descuentos_negativos_no_se_aceptan()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());

        var problema = await (await RegistrarAsync(operador, clienteId, Linea(await operador.CrearProductoAsync(8m), descuento: -1),
            descuento: -5)).ProblemaAsync(400, "validacion");

        var errores = problema.GetProperty("errors");
        Assert.True(errores.TryGetProperty("descuento", out _));
        Assert.True(errores.TryGetProperty("detalles[0].descuento", out _));
    }

    [Fact]
    public async Task Sin_direccion_ni_fecha_de_entrega_no_se_guarda()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var productoId = await operador.CrearProductoAsync(8m);

        var problema = await (await operador.PostAsJsonAsync("/api/pedidos", new
        {
            clienteId,
            direccionEntrega = "   ",
            detalles = Linea(productoId)
        })).ProblemaAsync(400, "validacion");

        var errores = problema.GetProperty("errors");
        Assert.True(errores.TryGetProperty("direccionEntrega", out _));
        Assert.True(errores.TryGetProperty("fechaEntregaEstimada", out _));
    }

    [Fact]
    public async Task Una_fecha_de_entrega_que_ya_paso_no_se_acepta()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var ayer = $"{HoraBolivia.Hoy(TimeProvider.System).AddDays(-1):yyyy-MM-dd}T10:00:00";

        await (await RegistrarAsync(operador, clienteId, Linea(await operador.CrearProductoAsync(8m)), fechaEntregaEstimada: ayer))
            .ProblemaAsync(400, "fecha_en_el_pasado");
    }

    [Fact]
    public async Task La_hora_de_entrega_sin_zona_es_la_de_Bolivia()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var manana = HoraBolivia.Hoy(TimeProvider.System).AddDays(1);

        var pedido = await (await RegistrarAsync(operador, clienteId, Linea(await operador.CrearProductoAsync(8m)),
            fechaEntregaEstimada: $"{manana:yyyy-MM-dd}T14:30:00")).LeerJsonAsync();

        // Las 14:30 de Bolivia (UTC−4) son las 18:30 en UTC, que es como se guarda.
        Assert.Equal(manana.ToDateTime(new TimeOnly(18, 30)), pedido.GetProperty("fechaEntregaEstimada").GetDateTime());
    }

    [Fact]
    public async Task El_filtro_por_dia_de_entrega_usa_el_dia_de_Bolivia()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var manana = HoraBolivia.Hoy(TimeProvider.System).AddDays(1);
        // A las 22:00 de Bolivia ya es el día siguiente en UTC, pero la entrega es de mañana.
        var pedido = await (await RegistrarAsync(operador, clienteId, Linea(await operador.CrearProductoAsync(8m)),
            fechaEntregaEstimada: $"{manana:yyyy-MM-dd}T22:00:00")).LeerJsonAsync();
        var pedidoId = pedido.GetProperty("id").GetInt32();

        Assert.Equal([pedidoId], await IdsAsync(operador, $"?clienteId={clienteId}&desde={manana:yyyy-MM-dd}&hasta={manana:yyyy-MM-dd}"));
        Assert.Empty(await IdsAsync(operador, $"?clienteId={clienteId}&desde={manana.AddDays(1):yyyy-MM-dd}"));
    }
}
