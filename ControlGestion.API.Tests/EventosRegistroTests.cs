using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class EventosRegistroTests(ApiDePrueba api)
{
    private static Task<HttpResponseMessage> RegistrarAsync(HttpClient cliente, int? clienteId = null, int? pedidoId = null,
        int tipoEventoId = 3, decimal? latitud = -17.7833m, decimal? longitud = -63.1821m) =>
        cliente.PostAsJsonAsync("/api/eventos", new
        {
            tipoEventoId, clienteId, pedidoId, latitud, longitud, descripcion = "  El portón estaba cerrado  "
        });

    private static async Task<JsonElement> ErroresAsync(HttpResponseMessage respuesta) =>
        (await respuesta.ProblemaAsync(400, "validacion")).GetProperty("errors");

    [Fact]
    public async Task Queda_pendiente_a_nombre_de_quien_lo_registra_con_su_ubicacion()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id));
        using var cliente = api.ClienteCon(promotor.Token);
        var antes = DateTime.UtcNow.AddSeconds(-5);

        var respuesta = await RegistrarAsync(cliente, clienteId);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var evento = await respuesta.LeerJsonAsync();
        Assert.Equal(0, evento.GetProperty("estado").GetInt32());
        Assert.Equal(promotor.Id, evento.GetProperty("promotor").GetProperty("id").GetString());
        Assert.Equal(clienteId, evento.GetProperty("cliente").GetProperty("id").GetInt32());
        Assert.Equal(-17.7833m, evento.GetProperty("latitud").GetDecimal());
        Assert.Equal("El portón estaba cerrado", evento.GetProperty("descripcion").GetString());
        Assert.InRange(DateTime.SpecifyKind(evento.GetProperty("fechaEvento").GetDateTime(), DateTimeKind.Utc),
            antes, DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task Sin_cliente_ni_pedido_no_se_registra()
    {
        using var operador = await api.OperadorAsync();

        var errores = await ErroresAsync(await RegistrarAsync(operador));

        Assert.True(errores.TryGetProperty("clienteId", out _));
    }

    [Fact]
    public async Task Sin_ubicacion_fuera_de_rango_o_en_0_0_no_se_registra()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());

        Assert.True((await ErroresAsync(await RegistrarAsync(operador, clienteId, latitud: null))).TryGetProperty("latitud", out _));
        // T7.13: la latitud 200 terminaba en 500 al guardarla.
        Assert.True((await ErroresAsync(await RegistrarAsync(operador, clienteId, latitud: 200))).TryGetProperty("latitud", out _));
        Assert.True((await ErroresAsync(await RegistrarAsync(operador, clienteId, longitud: -181))).TryGetProperty("longitud", out _));
        Assert.True((await ErroresAsync(await RegistrarAsync(operador, clienteId, latitud: 0, longitud: 0))).TryGetProperty("latitud", out _));
    }

    [Fact]
    public async Task Un_tipo_que_no_existe_responde_400()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());

        // T7.14: terminaba en 500 por la FK.
        await (await RegistrarAsync(operador, clienteId, tipoEventoId: 999)).ProblemaAsync(400, "tipo_no_valido");
    }

    [Fact]
    public async Task El_cliente_y_el_pedido_tienen_que_ser_de_la_cartera_de_quien_registra()
    {
        using var operador = await api.OperadorAsync();
        var promotorA = await api.CrearUsuarioAsync(Roles.Promotor);
        var promotorB = await api.CrearUsuarioAsync(Roles.Promotor);
        var clienteDeB = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotorB.Id));
        var pedidoDeB = await operador.CrearPedidoAsync(clienteDeB, await operador.CrearProductoAsync(10m), 1);
        using var clienteA = api.ClienteCon(promotorA.Token);

        await (await RegistrarAsync(clienteA, clienteDeB)).ProblemaAsync(400, "cliente_no_existe");
        await (await RegistrarAsync(clienteA, pedidoId: pedidoDeB)).ProblemaAsync(400, "pedido_no_existe");
        await (await RegistrarAsync(clienteA, 999999)).ProblemaAsync(400, "cliente_no_existe");
    }

    [Fact]
    public async Task Con_el_pedido_el_cliente_es_el_del_pedido_y_otro_cliente_se_rechaza()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var clienteId = await operador.CrearClienteAsync(zonaId);
        var otroClienteId = await operador.CrearClienteAsync(zonaId);
        var pedidoId = await operador.CrearPedidoAsync(clienteId, await operador.CrearProductoAsync(10m), 1);

        var evento = await (await RegistrarAsync(operador, pedidoId: pedidoId)).LeerJsonAsync();

        Assert.Equal(clienteId, evento.GetProperty("cliente").GetProperty("id").GetInt32());
        Assert.Equal(pedidoId, evento.GetProperty("pedido").GetProperty("id").GetInt32());
        await (await RegistrarAsync(operador, otroClienteId, pedidoId)).ProblemaAsync(400, "cliente_no_coincide");
    }

    [Fact]
    public async Task Un_cliente_inactivo_puede_tener_un_evento()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        (await operador.PutAsJsonAsync($"/api/clientes/{clienteId}/estado", new { estadoClienteId = 4 })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Created, (await RegistrarAsync(operador, clienteId)).StatusCode);
    }
}
