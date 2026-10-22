using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

/// <summary>El límite de una zona (5.5): lo dibuja el gerente general.</summary>
[Collection(ColeccionApi.Nombre)]
public class LimitesDeZonaTests(ApiDePrueba api)
{
    private static object Vertice(decimal latitud, decimal longitud) => new { latitud, longitud };

    private static readonly object[] Triangulo =
        [Vertice(-17.78m, -63.19m), Vertice(-17.78m, -63.17m), Vertice(-17.76m, -63.18m)];

    private static List<(decimal, decimal)> VerticesDe(JsonElement zona) =>
        zona.GetProperty("limite").EnumerateArray()
            .Select(v => (v.GetProperty("latitud").GetDecimal(), v.GetProperty("longitud").GetDecimal())).ToList();

    [Fact]
    public async Task El_limite_se_guarda_y_uno_nuevo_reemplaza_al_anterior()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        Assert.Equal(JsonValueKind.Null, (await (await operador.GetAsync($"/api/zonas/{zonaId}")).LeerJsonAsync()).GetProperty("limite").ValueKind);

        // Cerrado (el último igual al primero) y con más decimales de los que se guardan: queda abierto y redondeado.
        var guardada = await (await operador.PutAsJsonAsync($"/api/zonas/{zonaId}/limite", new
        {
            vertices = new[] { Vertice(-17.7800001m, -63.19m), Vertice(-17.78m, -63.17m), Vertice(-17.76m, -63.18m), Vertice(-17.78m, -63.19m) }
        })).LeerJsonAsync();
        Assert.Equal([(-17.78m, -63.19m), (-17.78m, -63.17m), (-17.76m, -63.18m)], VerticesDe(guardada));

        var cuadrado = new[] { Vertice(-17.70m, -63.20m), Vertice(-17.70m, -63.10m), Vertice(-17.80m, -63.10m), Vertice(-17.80m, -63.20m) };
        (await operador.PutAsJsonAsync($"/api/zonas/{zonaId}/limite", new { vertices = cuadrado })).EnsureSuccessStatusCode();
        var leida = (await (await operador.GetAsync("/api/zonas")).LeerJsonAsync()).EnumerateArray().Single(z => z.GetProperty("id").GetInt32() == zonaId);
        Assert.Equal(4, VerticesDe(leida).Count);
    }

    [Fact]
    public async Task Hacen_falta_3_puntos_distintos_y_cada_uno_en_su_rango()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        async Task<JsonElement> ErroresAsync(object[] vertices) =>
            (await (await operador.PutAsJsonAsync($"/api/zonas/{zonaId}/limite", new { vertices })).ProblemaAsync(400, "validacion"))
                .GetProperty("errors");

        Assert.Equal("El límite necesita 3 puntos o más.",
            (await ErroresAsync([Vertice(-17.78m, -63.19m), Vertice(-17.76m, -63.18m)])).GetProperty("vertices")[0].GetString());
        Assert.Equal("Marca 3 puntos distintos o más.",
            (await ErroresAsync([Vertice(-17.78m, -63.19m), Vertice(-17.78m, -63.19m), Vertice(-17.76m, -63.18m)])).GetProperty("vertices")[0].GetString());
        Assert.Equal("La latitud debe estar entre -90 y 90.",
            (await ErroresAsync([Vertice(-17.78m, -63.19m), Vertice(95m, -63.17m), Vertice(-17.76m, -63.18m)])).GetProperty("vertices[1].latitud")[0].GetString());
    }

    [Fact]
    public async Task El_limite_se_quita()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        (await operador.PutAsJsonAsync($"/api/zonas/{zonaId}/limite", new { vertices = Triangulo })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent, (await operador.DeleteAsync($"/api/zonas/{zonaId}/limite")).StatusCode);
        Assert.Equal(JsonValueKind.Null, (await (await operador.GetAsync($"/api/zonas/{zonaId}")).LeerJsonAsync()).GetProperty("limite").ValueKind);
    }

    [Fact]
    public async Task Solo_lo_dibuja_quien_tiene_el_permiso()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        using var gerente = api.ClienteCon((await api.CrearUsuarioAsync(Roles.GerenteGeneral)).Token);
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var comoPromotor = api.ClienteCon(promotor.Token);

        Assert.Equal(HttpStatusCode.OK, (await gerente.PutAsJsonAsync($"/api/zonas/{zonaId}/limite", new { vertices = Triangulo })).StatusCode);
        await (await comoPromotor.PutAsJsonAsync($"/api/zonas/{zonaId}/limite", new { vertices = Triangulo })).ProblemaAsync(403, "sin_permiso");
        await (await comoPromotor.DeleteAsync($"/api/zonas/{zonaId}/limite")).ProblemaAsync(403, "sin_permiso");
        // El promotor ve el límite: la app lo va a mostrar en su mapa.
        Assert.Equal(3, VerticesDe(await (await comoPromotor.GetAsync($"/api/zonas/{zonaId}")).LeerJsonAsync()).Count);
    }
}
