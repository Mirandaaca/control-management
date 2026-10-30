using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

/// <summary>Avanzar varios pedidos a la vez (1.2): POST /api/pedidos/avanzar, todo o nada.</summary>
[Collection(ColeccionApi.Nombre)]
public class PedidosAvanzarVariosTests(ApiDePrueba api)
{
    private const int Pendiente = 1, EnProceso = 2, EnRuta = 3, Anulado = 5;

    private static async Task<JsonElement> PedidoDe(HttpClient cliente, int pedidoId) =>
        await (await cliente.GetAsync($"/api/pedidos/{pedidoId}")).LeerJsonAsync();

    private static int EstadoDe(JsonElement pedido) => pedido.GetProperty("estado").GetProperty("id").GetInt32();

    private static Task<HttpResponseMessage> AvanzarVariosAsync(HttpClient cliente, string? observacion,
        params (int Id, int EstadoId)[] pedidos) =>
        cliente.PostAsJsonAsync("/api/pedidos/avanzar",
            new { pedidos = pedidos.Select(p => new { id = p.Id, estadoId = p.EstadoId }), observacion });

    private static async Task AvanzarUnoAsync(HttpClient cliente, int pedidoId) =>
        (await cliente.PostAsJsonAsync($"/api/pedidos/{pedidoId}/avanzar", new { })).EnsureSuccessStatusCode();

    private static async Task<int> PedidoNuevoAsync(HttpClient operador, int? zonaId = null) =>
        await operador.CrearPedidoAsync(await operador.CrearClienteAsync(zonaId ?? await operador.CrearZonaAsync()),
            await operador.CrearProductoAsync(10m), 1);

    [Fact]
    public async Task Cada_pedido_pasa_a_su_estado_siguiente_con_la_observacion_en_el_historial()
    {
        using var operador = await api.OperadorAsync();
        var primero = await PedidoNuevoAsync(operador);
        var segundo = await PedidoNuevoAsync(operador);
        var enProceso = await PedidoNuevoAsync(operador);
        await AvanzarUnoAsync(operador, enProceso);

        var avanzados = (await (await AvanzarVariosAsync(operador, "  Salen en la camioneta  ",
                (primero, Pendiente), (segundo, Pendiente), (enProceso, EnProceso))).LeerJsonAsync())
            .EnumerateArray().ToDictionary(p => p.GetProperty("id").GetInt32(), EstadoDe);

        Assert.Equal(new Dictionary<int, int> { [primero] = EnProceso, [segundo] = EnProceso, [enProceso] = EnRuta }, avanzados);
        var entrada = (await PedidoDe(operador, enProceso)).GetProperty("historial").EnumerateArray().Last();
        Assert.Equal(EnRuta, entrada.GetProperty("estado").GetProperty("id").GetInt32());
        Assert.Equal("Salen en la camioneta", entrada.GetProperty("observacion").GetString());
    }

    [Fact]
    public async Task Si_uno_cambio_mientras_se_elegian_no_avanza_ninguno()
    {
        using var operador = await api.OperadorAsync();
        var quieto = await PedidoNuevoAsync(operador);
        var movido = await PedidoNuevoAsync(operador);
        // Otra persona lo pasó a En proceso después de que se eligió como Pendiente: no debe saltar a En ruta.
        await AvanzarUnoAsync(operador, movido);

        var problema = await (await AvanzarVariosAsync(operador, null, (quieto, Pendiente), (movido, Pendiente)))
            .ProblemaAsync(409, "pedido_cambio");

        Assert.Contains((await PedidoDe(operador, movido)).GetProperty("numero").GetString()!,
            problema.GetProperty("detail").GetString());
        Assert.Equal(Pendiente, EstadoDe(await PedidoDe(operador, quieto)));
        Assert.Equal(EnProceso, EstadoDe(await PedidoDe(operador, movido)));
    }

    [Fact]
    public async Task Uno_en_ruta_o_anulado_no_avanza_y_los_demas_quedan_igual()
    {
        using var operador = await api.OperadorAsync();
        var pendiente = await PedidoNuevoAsync(operador);
        var enRuta = await PedidoNuevoAsync(operador);
        await AvanzarUnoAsync(operador, enRuta);
        await AvanzarUnoAsync(operador, enRuta);
        var anulado = await PedidoNuevoAsync(operador);
        (await operador.PostAsJsonAsync($"/api/pedidos/{anulado}/anular", new { motivo = "Prueba" })).EnsureSuccessStatusCode();

        await (await AvanzarVariosAsync(operador, null, (pendiente, Pendiente), (enRuta, EnRuta)))
            .ProblemaAsync(400, "transicion_no_permitida");
        await (await AvanzarVariosAsync(operador, null, (pendiente, Pendiente), (anulado, Anulado)))
            .ProblemaAsync(400, "pedido_en_estado_final");

        Assert.Equal(Pendiente, EstadoDe(await PedidoDe(operador, pendiente)));
        Assert.Single((await PedidoDe(operador, pendiente)).GetProperty("historial").EnumerateArray());
    }

    [Fact]
    public async Task Sin_pedidos_o_con_uno_repetido_responde_400()
    {
        using var operador = await api.OperadorAsync();
        var pedidoId = await PedidoNuevoAsync(operador);

        var vacia = await (await AvanzarVariosAsync(operador, null)).ProblemaAsync(400, "validacion");
        Assert.True(vacia.GetProperty("errors").TryGetProperty("pedidos", out _));
        await (await AvanzarVariosAsync(operador, null, (pedidoId, Pendiente), (pedidoId, Pendiente)))
            .ProblemaAsync(400, "pedido_repetido");
        Assert.Equal(Pendiente, EstadoDe(await PedidoDe(operador, pedidoId)));
    }

    [Fact]
    public async Task La_secretaria_los_avanza_y_el_promotor_no()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var pedidoId = await PedidoNuevoAsync(operador, await operador.CrearZonaAsync(promotor.Id));
        using var clientePromotor = api.ClienteCon(promotor.Token);
        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);

        await (await AvanzarVariosAsync(clientePromotor, null, (pedidoId, Pendiente))).ProblemaAsync(403, "sin_permiso");
        Assert.Equal(HttpStatusCode.OK, (await AvanzarVariosAsync(secretaria, null, (pedidoId, Pendiente))).StatusCode);
    }

    [Fact]
    public async Task Sin_cartera_completa_un_pedido_ajeno_responde_404_y_no_avanza_ninguno()
    {
        using var operador = await api.OperadorAsync();
        using var admin = await api.AdminAsync();
        var (_, rol) = await admin.CrearRolAsync(Permisos.Pedidos.CambiarEstado, Permisos.Territorial.RecorrerRuta);
        var otro = await api.CrearUsuarioAsync(rol);
        var suyo = await PedidoNuevoAsync(operador, await operador.CrearZonaAsync(otro.Id));
        var ajeno = await PedidoNuevoAsync(operador);
        using var clienteOtro = api.ClienteCon(otro.Token);

        await (await AvanzarVariosAsync(clienteOtro, null, (suyo, Pendiente), (ajeno, Pendiente))).ProblemaAsync(404, "no_encontrado");

        Assert.Equal(Pendiente, EstadoDe(await PedidoDe(operador, suyo)));
        Assert.Equal(Pendiente, EstadoDe(await PedidoDe(operador, ajeno)));
    }

    [Fact]
    public async Task Dos_pedidos_iguales_a_la_vez_avanzan_una_sola_vez()
    {
        using var operador = await api.OperadorAsync();
        var pedidoId = await PedidoNuevoAsync(operador);

        var respuestas = await Ayudantes.EnviarALaVezAsync(2, _ => AvanzarVariosAsync(operador, null, (pedidoId, Pendiente)));

        // La segunda choca con el token de concurrencia o ve que el pedido ya no está Pendiente: las dos son 409.
        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Conflict);
        var pedido = await PedidoDe(operador, pedidoId);
        Assert.Equal(EnProceso, EstadoDe(pedido));
        Assert.Equal(2, pedido.GetProperty("historial").GetArrayLength());
    }
}
