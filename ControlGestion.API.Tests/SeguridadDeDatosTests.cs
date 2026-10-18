using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class SeguridadDeDatosTests(ApiDePrueba api)
{
    private static readonly string[] CamposDeIdentity =
        ["passwordHash", "securityStamp", "concurrencyStamp", "normalizedEmail", "normalizedUserName"];

    [Fact]
    public async Task Ningun_usuario_anidado_expone_los_campos_internos_de_Identity()
    {
        var supervisor = await api.CrearUsuarioAsync("Supervisor");
        var promotor = await api.CrearUsuarioAsync("Promotor");
        using var admin = await api.AdminAsync();
        var zonaId = await admin.CrearZonaAsync(supervisor.Id);
        var productoId = await admin.CrearProductoAsync(10m);
        var clienteId = await admin.CrearClienteAsync(zonaId, promotor.Id);
        using var vendedor = api.ClienteCon(promotor.Token);
        var hojaId = await vendedor.CrearHojaPedidoAsync(clienteId, zonaId, productoId, 1, credito: false);

        foreach (var ruta in new[] { "/api/zonas", $"/api/clientes/{clienteId}", "/api/hojas-pedido", $"/api/hojas-pedido/{hojaId}",
                                     $"/api/inventario/{productoId}/movimientos" })
        {
            var cuerpo = await (await admin.GetAsync(ruta)).Content.ReadAsStringAsync();
            Assert.Contains("nombreCompleto", cuerpo);
            foreach (var campo in CamposDeIdentity)
                Assert.DoesNotContain($"\"{campo}\"", cuerpo, StringComparison.OrdinalIgnoreCase);
        }
    }
}
