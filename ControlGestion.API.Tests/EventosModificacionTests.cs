using System.Net;
using System.Net.Http.Json;
using ControlGestion.API.Data;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class EventosModificacionTests(ApiDePrueba api)
{
    private static Task<HttpResponseMessage> ModificarAsync(HttpClient cliente, int eventoId, int? clienteId = null,
        int? pedidoId = null, int tipoEventoId = 5, string descripcion = "Descripción corregida") =>
        cliente.PutAsJsonAsync($"/api/eventos/{eventoId}", new { tipoEventoId, clienteId, pedidoId, descripcion });

    private static Task<HttpResponseMessage> ResolverAsync(HttpClient cliente, int eventoId, string solucion = "Resuelto") =>
        cliente.PostAsJsonAsync($"/api/eventos/{eventoId}/resolver", new { solucion });

    [Fact]
    public async Task Quien_lo_registro_cambia_el_tipo_el_cliente_y_la_descripcion()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var zonaId = await operador.CrearZonaAsync(promotor.Id);
        var clienteId = await operador.CrearClienteAsync(zonaId);
        var otroClienteId = await operador.CrearClienteAsync(zonaId);
        using var cliente = api.ClienteCon(promotor.Token);
        var eventoId = await cliente.CrearEventoAsync(clienteId);
        var antes = await (await cliente.GetAsync($"/api/eventos/{eventoId}")).LeerJsonAsync();

        var evento = await (await ModificarAsync(cliente, eventoId, otroClienteId, descripcion: "  Era otro cliente  ")).LeerJsonAsync();

        Assert.Equal(5, evento.GetProperty("tipo").GetProperty("id").GetInt32());
        Assert.Equal(otroClienteId, evento.GetProperty("cliente").GetProperty("id").GetInt32());
        Assert.Equal("Era otro cliente", evento.GetProperty("descripcion").GetString());
        // La ubicación es la del registro: no cambia.
        Assert.Equal(antes.GetProperty("latitud").GetDecimal(), evento.GetProperty("latitud").GetDecimal());
    }

    [Fact]
    public async Task Con_el_pedido_el_cliente_pasa_a_ser_el_del_pedido()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var eventoId = await operador.CrearEventoAsync(await operador.CrearClienteAsync(zonaId));
        var clienteDelPedido = await operador.CrearClienteAsync(zonaId);
        var pedidoId = await operador.CrearPedidoAsync(clienteDelPedido, await operador.CrearProductoAsync(10m), 1);

        var evento = await (await ModificarAsync(operador, eventoId, pedidoId: pedidoId)).LeerJsonAsync();

        Assert.Equal(clienteDelPedido, evento.GetProperty("cliente").GetProperty("id").GetInt32());
        Assert.Equal(pedidoId, evento.GetProperty("pedido").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task El_promotor_no_cambia_el_evento_que_registro_otro_y_la_secretaria_cambia_cualquiera()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id));
        // Es de su cartera, así que lo ve, pero no lo registró él.
        var deOtro = await operador.CrearEventoAsync(clienteId);
        using var clientePromotor = api.ClienteCon(promotor.Token);
        var suyo = await clientePromotor.CrearEventoAsync(clienteId);

        await (await ModificarAsync(clientePromotor, deOtro, clienteId)).ProblemaAsync(400, "evento_de_otro_promotor");
        using var clienteSecretaria = api.ClienteCon(secretaria.Token);
        Assert.Equal(HttpStatusCode.OK, (await ModificarAsync(clienteSecretaria, suyo, clienteId)).StatusCode);
    }

    [Fact]
    public async Task Un_evento_resuelto_no_se_modifica()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var eventoId = await operador.CrearEventoAsync(clienteId);
        (await ResolverAsync(operador, eventoId)).EnsureSuccessStatusCode();

        await (await ModificarAsync(operador, eventoId, clienteId)).ProblemaAsync(400, "evento_resuelto");
    }

    [Fact]
    public async Task Dos_resoluciones_a_la_vez_solo_guardan_una()
    {
        using var operador = await api.OperadorAsync();
        var eventoId = await operador.CrearEventoAsync(await operador.CrearClienteAsync(await operador.CrearZonaAsync()));

        var respuestas = await Ayudantes.EnviarALaVezAsync(3, i => ResolverAsync(operador, eventoId, $"Solución {i}"));

        // La que llega tarde choca con el token (409) o ya lo ve resuelto (400); nunca se pisan dos soluciones.
        Assert.All(respuestas, r => Assert.Contains(r.StatusCode,
            new[] { HttpStatusCode.OK, HttpStatusCode.Conflict, HttpStatusCode.BadRequest }));
        var ganadora = Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.OK);
        var solucion = (await ganadora.LeerJsonAsync()).GetProperty("solucion").GetString();
        var evento = await (await operador.GetAsync($"/api/eventos/{eventoId}")).LeerJsonAsync();
        Assert.Equal(solucion, evento.GetProperty("solucion").GetString());
    }

    [Fact]
    public async Task Modificar_y_resolver_a_la_vez_nunca_responde_500()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var eventoId = await operador.CrearEventoAsync(clienteId);

        var respuestas = await Ayudantes.EnviarALaVezAsync(2,
            i => i == 0 ? ModificarAsync(operador, eventoId, clienteId) : ResolverAsync(operador, eventoId));

        Assert.All(respuestas, r => Assert.Contains(r.StatusCode,
            new[] { HttpStatusCode.OK, HttpStatusCode.Conflict, HttpStatusCode.BadRequest }));
    }

    [Fact]
    public async Task Un_cambio_sobre_una_version_vieja_del_evento_no_se_guarda()
    {
        using var operador = await api.OperadorAsync();
        var eventoId = await operador.CrearEventoAsync(await operador.CrearClienteAsync(await operador.CrearZonaAsync()));
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var leido = await db.Eventos.SingleAsync(e => e.Id == eventoId);
        // MySQL guarda el timestamp con microsegundos: una espera corta asegura que la versión cambie.
        await Task.Delay(5);

        (await ResolverAsync(operador, eventoId)).EnsureSuccessStatusCode();
        leido.Descripcion = "Cambio hecho sobre la versión que se leyó antes de resolver";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
    }
}
