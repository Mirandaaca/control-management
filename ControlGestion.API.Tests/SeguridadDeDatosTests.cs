using ControlGestion.API.Tests.Infraestructura;
using ControlGestion.API.Seguridad;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class SeguridadDeDatosTests(ApiDePrueba api)
{
    private static readonly string[] CamposDeIdentity =
        ["passwordHash", "securityStamp", "concurrencyStamp", "normalizedEmail", "normalizedUserName"];

    [Fact]
    public async Task Ningun_usuario_anidado_expone_los_campos_internos_de_Identity()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        // Una zona más, con el promotor de zonas de la corrida: /api/zonas trae a los dos anidados.
        await operador.CrearZonaAsync();
        var productoId = await operador.CrearProductoAsync(10m);
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id));
        using var vendedor = api.ClienteCon(promotor.Token);
        var hojaId = await vendedor.CrearHojaPedidoAsync(clienteId, productoId, 1, credito: false);
        var pedidoId = await vendedor.CrearPedidoAsync(clienteId, productoId, 1);

        foreach (var ruta in new[] { "/api/zonas", $"/api/clientes/{clienteId}", "/api/hojas-pedido", $"/api/hojas-pedido/{hojaId}",
                                     $"/api/inventario/{productoId}/movimientos", "/api/pedidos", $"/api/pedidos/{pedidoId}",
                                     $"/api/pedidos/{pedidoId}/historial" })
        {
            var cuerpo = await (await operador.GetAsync(ruta)).Content.ReadAsStringAsync();
            Assert.Contains("nombreCompleto", cuerpo);
            foreach (var campo in CamposDeIdentity)
                Assert.DoesNotContain($"\"{campo}\"", cuerpo, StringComparison.OrdinalIgnoreCase);
        }
    }
}
