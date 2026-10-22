using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

/// <summary>Las zonas de venta (7.2), que administra el gerente general.</summary>
[Collection(ColeccionApi.Nombre)]
public class ZonasTests(ApiDePrueba api)
{
    private static async Task<JsonElement> ZonaAsync(HttpClient cliente, int id) =>
        (await (await cliente.GetAsync("/api/zonas")).LeerJsonAsync()).EnumerateArray().Single(z => z.GetProperty("id").GetInt32() == id);

    [Fact]
    public async Task La_lista_trae_el_promotor_resumido_y_cuantos_clientes_ve_cada_usuario()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync(promotor.Id);
        await operador.CrearClienteAsync(zonaId);
        await operador.CrearClienteAsync(zonaId);
        var ajena = await operador.CrearZonaAsync();
        await operador.CrearClienteAsync(ajena);
        using var comoPromotor = api.ClienteCon(promotor.Token);

        var zona = await ZonaAsync(operador, zonaId);
        Assert.Equal(["id", "nombreCompleto"], zona.GetProperty("promotor").EnumerateObject().Select(p => p.Name));
        Assert.Equal(promotor.Id, zona.GetProperty("promotor").GetProperty("id").GetString());
        Assert.True(zona.GetProperty("promotorValido").GetBoolean());
        Assert.Equal(2, zona.GetProperty("clientes").GetInt32());

        // El promotor ve todas las zonas, pero solo cuenta los clientes de su cartera.
        Assert.Equal(2, (await ZonaAsync(comoPromotor, zonaId)).GetProperty("clientes").GetInt32());
        Assert.Equal(0, (await ZonaAsync(comoPromotor, ajena)).GetProperty("clientes").GetInt32());
        Assert.Equal(1, (await ZonaAsync(operador, ajena)).GetProperty("clientes").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await comoPromotor.GetAsync($"/api/zonas/{zonaId}")).StatusCode);
    }
}
