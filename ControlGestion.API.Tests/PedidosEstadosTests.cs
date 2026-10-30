using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class PedidosEstadosTests(ApiDePrueba api)
{
    private static async Task<JsonElement> PedidoDe(HttpClient cliente, int pedidoId) =>
        await (await cliente.GetAsync($"/api/pedidos/{pedidoId}")).LeerJsonAsync();

    private static int EstadoDe(JsonElement pedido) => pedido.GetProperty("estado").GetProperty("id").GetInt32();

    private static Task<HttpResponseMessage> AvanzarAsync(HttpClient cliente, int pedidoId, string? observacion = null) =>
        cliente.PostAsJsonAsync($"/api/pedidos/{pedidoId}/avanzar", new { observacion });

    private static Task<HttpResponseMessage> AnularAsync(HttpClient cliente, int pedidoId, string motivo = "El cliente ya no lo quiere") =>
        cliente.PostAsJsonAsync($"/api/pedidos/{pedidoId}/anular", new { motivo });

    private static async Task<int> PedidoNuevoAsync(HttpClient operador, int? zonaId = null) =>
        await operador.CrearPedidoAsync(await operador.CrearClienteAsync(zonaId ?? await operador.CrearZonaAsync()),
            await operador.CrearProductoAsync(10m), 1);

    [Fact]
    public async Task Avanzar_pasa_de_pendiente_a_en_ruta_y_ahi_se_detiene()
    {
        using var operador = await api.OperadorAsync();
        var pedidoId = await PedidoNuevoAsync(operador);

        Assert.Equal(3, EstadoDe(await (await AvanzarAsync(operador, pedidoId, "Sale en la camioneta")).LeerJsonAsync()));
        await (await AvanzarAsync(operador, pedidoId)).ProblemaAsync(400, "transicion_no_permitida");

        var historial = (await PedidoDe(operador, pedidoId)).GetProperty("historial").EnumerateArray().ToList();
        Assert.Equal([1, 3], historial.Select(h => h.GetProperty("estado").GetProperty("id").GetInt32()));
        Assert.Equal("Sale en la camioneta", historial[1].GetProperty("observacion").GetString());
    }

    [Fact]
    public async Task Avanzar_ya_no_asigna_al_repartidor()
    {
        using var operador = await api.OperadorAsync();
        var pedidoId = await PedidoNuevoAsync(operador);

        var pedido = await (await AvanzarAsync(operador, pedidoId, "Con observación")).LeerJsonAsync();

        Assert.Equal(JsonValueKind.Null, pedido.GetProperty("repartidor").ValueKind);
    }

    [Fact]
    public async Task Anular_pide_un_motivo_y_lo_deja_en_el_historial()
    {
        using var operador = await api.OperadorAsync();
        var pedidoId = await PedidoNuevoAsync(operador);

        var problema = await (await operador.PostAsJsonAsync($"/api/pedidos/{pedidoId}/anular", new { motivo = " " }))
            .ProblemaAsync(400, "validacion");
        Assert.True(problema.GetProperty("errors").TryGetProperty("motivo", out _));

        var anulado = await (await AnularAsync(operador, pedidoId, "  El cliente cerró su tienda  ")).LeerJsonAsync();
        Assert.Equal(5, EstadoDe(anulado));
        var entrada = anulado.GetProperty("historial").EnumerateArray().Last();
        Assert.Equal(5, entrada.GetProperty("estado").GetProperty("id").GetInt32());
        Assert.Equal("El cliente cerró su tienda", entrada.GetProperty("observacion").GetString());
    }

    [Fact]
    public async Task Un_pedido_anulado_no_se_anula_ni_avanza()
    {
        using var operador = await api.OperadorAsync();
        var pedidoId = await PedidoNuevoAsync(operador);
        (await AnularAsync(operador, pedidoId)).EnsureSuccessStatusCode();

        await (await AnularAsync(operador, pedidoId)).ProblemaAsync(400, "pedido_en_estado_final");
        await (await AvanzarAsync(operador, pedidoId)).ProblemaAsync(400, "pedido_en_estado_final");
    }

    [Fact]
    public async Task La_secretaria_avanza_y_anula_y_el_promotor_no()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var pedidoId = await PedidoNuevoAsync(operador, await operador.CrearZonaAsync(promotor.Id));
        using var clientePromotor = api.ClienteCon(promotor.Token);
        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);

        await (await AvanzarAsync(clientePromotor, pedidoId)).ProblemaAsync(403, "sin_permiso");
        await (await AnularAsync(clientePromotor, pedidoId)).ProblemaAsync(403, "sin_permiso");
        Assert.Equal(HttpStatusCode.OK, (await AvanzarAsync(secretaria, pedidoId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AnularAsync(secretaria, pedidoId)).StatusCode);
    }

    [Fact]
    public async Task Sin_cartera_completa_un_pedido_ajeno_responde_404_al_avanzarlo_o_anularlo()
    {
        using var operador = await api.OperadorAsync();
        var pedidoId = await PedidoNuevoAsync(operador);
        using var admin = await api.AdminAsync();
        var (_, rol) = await admin.CrearRolAsync(Permisos.Pedidos.CambiarEstado, Permisos.Pedidos.AnularPedido);
        using var otro = api.ClienteCon((await api.CrearUsuarioAsync(rol)).Token);

        await (await AvanzarAsync(otro, pedidoId)).ProblemaAsync(404, "no_encontrado");
        await (await AnularAsync(otro, pedidoId)).ProblemaAsync(404, "no_encontrado");
        Assert.Equal(1, EstadoDe(await PedidoDe(operador, pedidoId)));
    }

    [Fact]
    public async Task Dos_avances_a_la_vez_lo_avanzan_una_sola_vez()
    {
        using var operador = await api.OperadorAsync();
        var pedidoId = await PedidoNuevoAsync(operador);

        var respuestas = await Ayudantes.EnviarALaVezAsync(2, _ => AvanzarAsync(operador, pedidoId));

        // Si las dos leyeron "Pendiente", la segunda choca con el token (409). Si no, ve que ya está En ruta (400).
        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(respuestas, r => r.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest);
        var pedido = await PedidoDe(operador, pedidoId);
        Assert.Equal(3, EstadoDe(pedido));
        Assert.Equal(2, pedido.GetProperty("historial").GetArrayLength());
    }
}
