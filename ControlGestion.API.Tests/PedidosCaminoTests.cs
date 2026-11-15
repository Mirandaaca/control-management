using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>
/// La ubicación del cliente en el detalle del pedido y el camino hasta él para entregarlo, como el de una visita de la
/// ruta (tarea 25), con un OpenRouteService falso. Cada usuario pide un solo camino por prueba, salvo la del límite.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class PedidosCaminoTests(ApiDePrueba api)
{
    private const string DesdeElOrigen = "latitud=-17.78&longitud=-63.19";
    private readonly string _clave = $"clave-de-prueba-{Guid.NewGuid():N}";

    private sealed record Entrega(UsuarioDePrueba Promotor, int PedidoId, int ClienteId, int ZonaId);

    /// <summary>Un promotor con su zona y el pedido de un cliente de ella, asignado a él; En ruta, salvo que se pida otra cosa.</summary>
    private async Task<Entrega> PedidoAsignadoAsync(bool enRuta = true)
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync(promotor.Id);
        var clienteId = await operador.CrearClienteAsync(zonaId);
        var pedidoId = await PedidoParaAsync(operador, promotor, clienteId, enRuta);
        return new Entrega(promotor, pedidoId, clienteId, zonaId);
    }

    private static async Task<int> PedidoParaAsync(HttpClient operador, UsuarioDePrueba promotor, int clienteId, bool enRuta = true)
    {
        var pedidoId = await operador.CrearPedidoAsync(clienteId, await operador.CrearProductoAsync(10m), 1);
        (await operador.PostAsJsonAsync("/api/pedidos/asignar", new
        {
            pedidoIds = new[] { pedidoId }, promotorId = promotor.Id,
            dia = HoraBolivia.Hoy(TimeProvider.System).AddDays(1).ToString("yyyy-MM-dd")
        })).EnsureSuccessStatusCode();
        if (enRuta)
            (await operador.PostAsJsonAsync($"/api/pedidos/{pedidoId}/avanzar", new { })).EnsureSuccessStatusCode();
        return pedidoId;
    }

    private static string Camino(int pedidoId, string consulta = DesdeElOrigen) => $"/api/pedidos/{pedidoId}/camino?{consulta}";

    private static async Task<HttpResponseMessage> PedirAsync(WebApplicationFactory<Program> fabrica, UsuarioDePrueba usuario,
        int pedidoId, string consulta = DesdeElOrigen)
    {
        using var cliente = fabrica.ClienteCon(usuario.Token);
        return await cliente.GetAsync(Camino(pedidoId, consulta));
    }

    private async Task UbicarClienteAsync(int clienteId, decimal latitud, decimal longitud)
    {
        using var scope = api.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Clientes.Where(c => c.Id == clienteId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Latitud, latitud).SetProperty(c => c.Longitud, longitud));
    }

    private static decimal[] Coordenada(JsonElement cuerpo, int cual) =>
        cuerpo.GetProperty("coordinates")[cual].EnumerateArray().Select(c => c.GetDecimal()).ToArray();

    [Fact]
    public async Task El_detalle_del_pedido_trae_la_ubicacion_del_cliente()
    {
        var e = await PedidoAsignadoAsync();
        await UbicarClienteAsync(e.ClienteId, -17.7712m, -63.1745m);
        using var promotor = api.ClienteCon(e.Promotor.Token);

        var pedido = await (await promotor.GetAsync($"/api/pedidos/{e.PedidoId}")).LeerJsonAsync();

        var ubicacion = pedido.GetProperty("ubicacionDelCliente");
        Assert.Equal(-17.7712m, ubicacion.GetProperty("latitud").GetDecimal());
        Assert.Equal(-63.1745m, ubicacion.GetProperty("longitud").GetDecimal());
    }

    [Fact]
    public async Task El_camino_va_del_celular_al_cliente_de_la_base()
    {
        var proveedor = new OpenRouteServiceFalso();
        await using var fabrica = api.ConNavegacion(proveedor, _clave);
        var e = await PedidoAsignadoAsync();
        await UbicarClienteAsync(e.ClienteId, -17.7701m, -63.1702m);

        var respuesta = await PedirAsync(fabrica, e.Promotor, e.PedidoId, $"{DesdeElOrigen}&medio=pie");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var caminos = await respuesta.LeerJsonAsync();
        Assert.Equal(1202, caminos.GetProperty("principal").GetProperty("distanciaMetros").GetInt32());
        Assert.Single(caminos.GetProperty("alternativas").EnumerateArray());
        var pedido = Assert.Single(proveedor.Pedidos);
        Assert.Equal("/v2/directions/foot-walking/json", pedido.Url.AbsolutePath);
        // [longitud, latitud]: el origen del celular y el destino del cliente en la base.
        Assert.Equal(new[] { -63.19m, -17.78m }, Coordenada(pedido.Cuerpo, 0));
        Assert.Equal(new[] { -63.1702m, -17.7701m }, Coordenada(pedido.Cuerpo, 1));
    }

    [Fact]
    public async Task El_promotor_del_cliente_que_no_lo_entrega_no_pide_el_camino()
    {
        var duenoDelCliente = await api.CrearUsuarioAsync(Roles.Promotor);
        var asignado = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(duenoDelCliente.Id));
        var pedidoId = await PedidoParaAsync(operador, asignado, clienteId);

        await (await PedirAsync(api, duenoDelCliente, pedidoId)).ProblemaAsync(400, "pedido_de_otro_promotor");
    }

    [Fact]
    public async Task Otro_promotor_no_ve_el_camino_de_un_pedido_ajeno()
    {
        var e = await PedidoAsignadoAsync();
        var otro = await api.CrearUsuarioAsync(Roles.Promotor);

        await (await PedirAsync(api, otro, e.PedidoId)).ProblemaAsync(404);
    }

    [Fact]
    public async Task Un_pedido_que_no_salio_a_ruta_no_tiene_camino()
    {
        var e = await PedidoAsignadoAsync(enRuta: false);

        await (await PedirAsync(api, e.Promotor, e.PedidoId)).ProblemaAsync(400, "pedido_no_esta_en_ruta");
    }

    [Fact]
    public async Task Un_pedido_entregado_no_tiene_camino()
    {
        var e = await PedidoAsignadoAsync();
        using (var promotor = api.ClienteCon(e.Promotor.Token))
            (await promotor.PostAsJsonAsync($"/api/pedidos/{e.PedidoId}/entregar", new
            {
                recibidoPor = "Doña Rosa", tipoVenta = 0, latitud = -17.7801m, longitud = -63.1795m, precisionGPS = 8
            })).EnsureSuccessStatusCode();

        await (await PedirAsync(api, e.Promotor, e.PedidoId)).ProblemaAsync(400, "pedido_en_estado_final");
    }

    [Fact]
    public async Task Sin_el_permiso_de_entregar_no_hay_camino()
    {
        var e = await PedidoAsignadoAsync();
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);

        await (await PedirAsync(api, secretaria, e.PedidoId)).ProblemaAsync(403);
    }

    // El cupo del proveedor es uno solo: un camino cada 10 s por usuario, sea de una visita de la ruta o de un pedido.
    [Fact]
    public async Task El_limite_es_el_mismo_que_el_del_camino_de_la_ruta()
    {
        var proveedor = new OpenRouteServiceFalso();
        await using var fabrica = api.ConNavegacion(proveedor, _clave);
        var e = await PedidoAsignadoAsync();
        using var operador = await api.OperadorAsync();
        var ruta = await (await operador.PostAsJsonAsync("/api/rutas", new
        {
            zonaId = e.ZonaId, promotorId = e.Promotor.Id,
            fecha = HoraBolivia.Hoy(TimeProvider.System).ToString("yyyy-MM-dd"),
            clientes = new[] { await operador.CrearClienteAsync(e.ZonaId) }
        })).LeerJsonAsync();
        var rutaId = ruta.GetProperty("id").GetInt32();
        var visitaId = ruta.GetProperty("visitas")[0].GetProperty("id").GetInt32();
        using var promotor = fabrica.ClienteCon(e.Promotor.Token);
        (await promotor.PostAsync($"/api/rutas/{rutaId}/iniciar", null)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK,
            (await promotor.GetAsync($"/api/rutas/{rutaId}/visitas/{visitaId}/camino?{DesdeElOrigen}")).StatusCode);
        await (await promotor.GetAsync(Camino(e.PedidoId, "latitud=-17.75&longitud=-63.15")))
            .ProblemaAsync(503, NavegacionService.CodigoNoDisponible);

        Assert.Single(proveedor.Pedidos);
    }
}
