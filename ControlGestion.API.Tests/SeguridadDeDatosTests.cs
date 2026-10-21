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
        var gerente = await api.CrearUsuarioAsync(Roles.GerenteGeneral);
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync(gerente.Id);
        var productoId = await operador.CrearProductoAsync(10m);
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id));
        using var vendedor = api.ClienteCon(promotor.Token);
        var hojaId = await vendedor.CrearHojaPedidoAsync(clienteId, productoId, 1, credito: false);

        foreach (var ruta in new[] { "/api/zonas", $"/api/clientes/{clienteId}", "/api/hojas-pedido", $"/api/hojas-pedido/{hojaId}",
                                     $"/api/inventario/{productoId}/movimientos" })
        {
            var cuerpo = await (await operador.GetAsync(ruta)).Content.ReadAsStringAsync();
            Assert.Contains("nombreCompleto", cuerpo);
            foreach (var campo in CamposDeIdentity)
                Assert.DoesNotContain($"\"{campo}\"", cuerpo, StringComparison.OrdinalIgnoreCase);
        }
    }
}
