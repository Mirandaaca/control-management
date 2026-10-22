using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

/// <summary>Los puntos de referencia de las zonas (5.5): los registra el gerente general y los ven todos.</summary>
[Collection(ColeccionApi.Nombre)]
public class PuntosReferenciaTests(ApiDePrueba api)
{
    private static object Punto(int zonaId, string? nombre = null, decimal latitud = -17.7833m, decimal longitud = -63.1821m) =>
        new { zonaId, nombre = nombre ?? Ayudantes.Unico("Mercado"), descripcion = "Entrada por la avenida", latitud, longitud };

    private static async Task<List<int>> IdsAsync(HttpClient cliente, int zonaId) =>
        (await (await cliente.GetAsync($"/api/puntos-referencia?zonaId={zonaId}")).LeerJsonAsync())
            .EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).ToList();

    [Fact]
    public async Task Un_punto_se_registra_se_mueve_a_otra_zona_y_se_elimina()
    {
        using var gerente = api.ClienteCon((await api.CrearUsuarioAsync(Roles.GerenteGeneral)).Token);
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var otraZona = await operador.CrearZonaAsync();

        var respuesta = await gerente.PostAsJsonAsync("/api/puntos-referencia", Punto(zonaId, "  Mercado Los Pozos  "));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creado = await respuesta.LeerJsonAsync();
        var id = creado.GetProperty("id").GetInt32();
        Assert.Equal("Mercado Los Pozos", creado.GetProperty("nombre").GetString());
        Assert.Equal(zonaId, creado.GetProperty("zona").GetProperty("id").GetInt32());
        Assert.Equal([id], await IdsAsync(gerente, zonaId));
        Assert.Equal(1, (await (await operador.GetAsync($"/api/zonas/{zonaId}")).LeerJsonAsync()).GetProperty("puntosDeReferencia").GetInt32());

        var movido = await (await gerente.PutAsJsonAsync($"/api/puntos-referencia/{id}", Punto(otraZona, "Mercado Abasto"))).LeerJsonAsync();
        Assert.Equal(otraZona, movido.GetProperty("zona").GetProperty("id").GetInt32());
        Assert.Empty(await IdsAsync(gerente, zonaId));

        Assert.Equal(HttpStatusCode.NoContent, (await gerente.DeleteAsync($"/api/puntos-referencia/{id}")).StatusCode);
        await (await gerente.GetAsync($"/api/puntos-referencia/{id}")).ProblemaAsync(404, "no_encontrado");
    }

    [Fact]
    public async Task Pide_nombre_una_ubicacion_valida_y_una_zona_que_exista()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        async Task<JsonElement> ErroresAsync(object punto) =>
            (await (await operador.PostAsJsonAsync("/api/puntos-referencia", punto)).ProblemaAsync(400, "validacion")).GetProperty("errors");

        Assert.True((await ErroresAsync(Punto(zonaId, nombre: ""))).TryGetProperty("nombre", out _));
        Assert.Equal("Marca el punto en el mapa.", (await ErroresAsync(Punto(zonaId, latitud: 0, longitud: 0))).GetProperty("latitud")[0].GetString());
        Assert.True((await ErroresAsync(Punto(zonaId, latitud: 95))).TryGetProperty("latitud", out _));
        Assert.Equal("La zona elegida no existe.", (await ErroresAsync(Punto(999999))).GetProperty("zonaId")[0].GetString());
    }

    [Fact]
    public async Task Todos_los_ven_y_solo_el_gerente_los_registra_modifica_o_elimina()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var id = (await (await operador.PostAsJsonAsync("/api/puntos-referencia", Punto(zonaId))).LeerJsonAsync()).GetProperty("id").GetInt32();
        using var promotor = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Promotor)).Token);
        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);

        Assert.Equal([id], await IdsAsync(promotor, zonaId));
        Assert.Equal([id], await IdsAsync(secretaria, zonaId));
        await (await promotor.PostAsJsonAsync("/api/puntos-referencia", Punto(zonaId))).ProblemaAsync(403, "sin_permiso");
        await (await promotor.PutAsJsonAsync($"/api/puntos-referencia/{id}", Punto(zonaId))).ProblemaAsync(403, "sin_permiso");
        await (await secretaria.DeleteAsync($"/api/puntos-referencia/{id}")).ProblemaAsync(403, "sin_permiso");
    }

    [Fact]
    public async Task Eliminar_una_zona_libre_borra_con_ella_su_limite_y_sus_puntos()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        (await operador.PutAsJsonAsync($"/api/zonas/{zonaId}/limite", new
        {
            vertices = new[] { new { latitud = -17.78m, longitud = -63.19m }, new { latitud = -17.78m, longitud = -63.17m }, new { latitud = -17.76m, longitud = -63.18m } }
        })).EnsureSuccessStatusCode();
        var puntoId = (await (await operador.PostAsJsonAsync("/api/puntos-referencia", Punto(zonaId))).LeerJsonAsync()).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.NoContent, (await operador.DeleteAsync($"/api/zonas/{zonaId}")).StatusCode);
        await (await operador.GetAsync($"/api/puntos-referencia/{puntoId}")).ProblemaAsync(404, "no_encontrado");
    }
}
