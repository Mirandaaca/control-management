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

    [Fact]
    public async Task El_promotor_es_obligatorio_recorre_rutas_y_esta_activo()
    {
        var gerente = await api.CrearUsuarioAsync(Roles.GerenteGeneral);
        var inactivo = await api.CrearUsuarioAsync(Roles.Promotor);
        var valido = await api.CrearUsuarioAsync(Roles.Promotor);
        using var admin = await api.AdminAsync();
        (await admin.PutAsync($"/api/usuarios/{inactivo.Id}/desactivar", null)).EnsureSuccessStatusCode();
        using var operador = await api.OperadorAsync();
        object Zona(string? promotorId) => new { nombre = Ayudantes.Unico("Zona"), codigo = Ayudantes.Unico("Z"), promotorId };

        var sinPromotor = await (await operador.PostAsJsonAsync("/api/zonas", Zona(null))).ProblemaAsync(400, "validacion");
        Assert.True(sinPromotor.GetProperty("errors").TryGetProperty("promotorId", out _));
        foreach (var invalido in new[] { gerente.Id, inactivo.Id, "no-existe" })
        {
            var problema = await (await operador.PostAsJsonAsync("/api/zonas", Zona(invalido))).ProblemaAsync(400, "validacion");
            Assert.Equal("Elige un promotor activo, que recorra rutas.", problema.GetProperty("errors").GetProperty("promotorId")[0].GetString());
        }

        var creada = await operador.PostAsJsonAsync("/api/zonas", Zona(valido.Id));
        Assert.Equal(HttpStatusCode.Created, creada.StatusCode);
        var id = (await creada.LeerJsonAsync()).GetProperty("id").GetInt32();
        // Al modificar también: cambiar al gerente se rechaza.
        await (await operador.PutAsJsonAsync($"/api/zonas/{id}", new { nombre = "Otra", promotorId = gerente.Id })).ProblemaAsync(400, "validacion");
    }

    [Fact]
    public async Task El_codigo_y_el_nombre_no_se_repiten_y_el_codigo_no_cambia()
    {
        using var operador = await api.OperadorAsync();
        var codigo = Ayudantes.Unico("Z");
        var nombre = Ayudantes.Unico("Zona Única");
        var creada = await operador.PostAsJsonAsync("/api/zonas", new { nombre, codigo, promotorId = ApiDePrueba.PromotorDeZonasId });
        var id = (await creada.LeerJsonAsync()).GetProperty("id").GetInt32();
        var otra = await operador.CrearZonaAsync();

        await (await operador.PostAsJsonAsync("/api/zonas", new { nombre = Ayudantes.Unico("Zona"), codigo, promotorId = ApiDePrueba.PromotorDeZonasId }))
            .ProblemaAsync(409, "zona_codigo_repetido");
        // Sin importar mayúsculas ni tildes, como el resto de la base.
        await (await operador.PostAsJsonAsync("/api/zonas", new { nombre = nombre.ToUpperInvariant().Replace("Ú", "U"), codigo = Ayudantes.Unico("Z"),
            promotorId = ApiDePrueba.PromotorDeZonasId })).ProblemaAsync(409, "zona_nombre_repetido");
        await (await operador.PutAsJsonAsync($"/api/zonas/{otra}", new { nombre, promotorId = ApiDePrueba.PromotorDeZonasId }))
            .ProblemaAsync(409, "zona_nombre_repetido");

        // El PUT no recibe el código: aunque llegue otro, no cambia.
        var modificada = await (await operador.PutAsJsonAsync($"/api/zonas/{id}", new { nombre, codigo = "OTRO", promotorId = ApiDePrueba.PromotorDeZonasId }))
            .LeerJsonAsync();
        Assert.Equal(codigo, modificada.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Una_zona_cuyo_promotor_se_desactivo_queda_marcada_y_se_corrige_al_elegir_otro()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var nuevo = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync(promotor.Id);
        using var admin = await api.AdminAsync();
        (await admin.PutAsync($"/api/usuarios/{promotor.Id}/desactivar", null)).EnsureSuccessStatusCode();

        Assert.False((await ZonaAsync(operador, zonaId)).GetProperty("promotorValido").GetBoolean());
        var corregida = await (await operador.PutAsJsonAsync($"/api/zonas/{zonaId}", new { nombre = "Zona corregida", promotorId = nuevo.Id }))
            .LeerJsonAsync();
        Assert.True(corregida.GetProperty("promotorValido").GetBoolean());
    }
}
