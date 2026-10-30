using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

/// <summary>La administración de los estados del cliente (7.4), que hace el administrador del sistema.</summary>
[Collection(ColeccionApi.Nombre)]
public class EstadosClienteTests(ApiDePrueba api)
{
    private const string Azul = "#1D4ED8";
    private const string Violeta = "#6D28D9";

    private static async Task<JsonElement> RegistrarAsync(HttpClient admin, string? nombre = null, string color = Azul)
    {
        var respuesta = await admin.PostAsJsonAsync("/api/estados-cliente",
            new { nombre = nombre ?? Ayudantes.Unico("PAR Estado"), color, descripcion = "Estado de prueba" });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return await respuesta.LeerJsonAsync();
    }

    private static Task<HttpResponseMessage> ModificarAsync(HttpClient admin, int id, string nombre, string color, string? descripcion = null) =>
        admin.PutAsJsonAsync($"/api/estados-cliente/{id}", new { nombre, color, descripcion });

    private static async Task<JsonElement> EstadoAsync(HttpClient admin, int id) =>
        await (await admin.GetAsync($"/api/estados-cliente/{id}")).LeerJsonAsync();

    [Fact]
    public async Task Un_estado_manual_se_registra_se_modifica_y_se_elimina()
    {
        using var admin = await api.AdminAsync();

        var creado = await RegistrarAsync(admin, color: "  #1d4ed8 ");
        var id = creado.GetProperty("id").GetInt32();
        Assert.Equal(Azul, creado.GetProperty("color").GetString());
        Assert.False(creado.GetProperty("esDelSistema").GetBoolean());
        Assert.False(creado.GetProperty("protegido").GetBoolean());
        Assert.Equal(0, creado.GetProperty("clientes").GetInt32());

        var nombre = Ayudantes.Unico("PAR Cerrado");
        var modificado = await (await ModificarAsync(admin, id, nombre, Violeta, "  ")).LeerJsonAsync();
        Assert.Equal((nombre, Violeta), (modificado.GetProperty("nombre").GetString(), modificado.GetProperty("color").GetString()));
        Assert.Equal(JsonValueKind.Null, modificado.GetProperty("descripcion").ValueKind);
        var lista = (await (await admin.GetAsync("/api/estados-cliente")).LeerJsonAsync()).EnumerateArray();
        Assert.Contains(lista, e => e.GetProperty("id").GetInt32() == id);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/estados-cliente/{id}")).StatusCode);
        await (await admin.GetAsync($"/api/estados-cliente/{id}")).ProblemaAsync(404, "no_encontrado");
    }

    [Fact]
    public async Task El_color_es_de_la_paleta_y_nunca_el_rojo_ni_el_verde_del_sistema()
    {
        using var admin = await api.AdminAsync();
        var id = (await RegistrarAsync(admin)).GetProperty("id").GetInt32();

        foreach (var color in new[] { PaletaDeEstados.Activo, PaletaDeEstados.Disponible, "#123456", "rojo" })
        {
            var alRegistrar = await (await admin.PostAsJsonAsync("/api/estados-cliente", new { nombre = Ayudantes.Unico("PAR"), color }))
                .ProblemaAsync(400, "validacion");
            Assert.Equal("Elige un color de la paleta.", alRegistrar.GetProperty("errors").GetProperty("color")[0].GetString());
            await (await ModificarAsync(admin, id, (await EstadoAsync(admin, id)).GetProperty("nombre").GetString()!, color))
                .ProblemaAsync(400, "validacion");
        }
    }

    [Fact]
    public async Task El_nombre_no_se_repite_sin_importar_mayusculas_ni_tildes()
    {
        using var admin = await api.AdminAsync();
        var nombre = Ayudantes.Unico("PAR Moroso");
        var otro = (await RegistrarAsync(admin)).GetProperty("id").GetInt32();
        await RegistrarAsync(admin, nombre);

        await (await admin.PostAsJsonAsync("/api/estados-cliente", new { nombre = "ináctivo", color = Azul }))
            .ProblemaAsync(409, "estado_repetido");
        await (await ModificarAsync(admin, otro, nombre.ToUpperInvariant(), Azul)).ProblemaAsync(409, "estado_repetido");
    }

    [Fact]
    public async Task Activo_y_Disponible_solo_cambian_su_descripcion()
    {
        using var admin = await api.AdminAsync();
        var activo = await EstadoAsync(admin, EstadosDeCliente.Activo);
        var descripcion = activo.GetProperty("descripcion").GetString();
        Assert.True(activo.GetProperty("esDelSistema").GetBoolean());
        Assert.True(activo.GetProperty("protegido").GetBoolean());

        try
        {
            var conOtraDescripcion = await (await ModificarAsync(admin, EstadosDeCliente.Activo, "Activo", PaletaDeEstados.Activo,
                "Visitado hace poco")).LeerJsonAsync();
            Assert.Equal("Visitado hace poco", conOtraDescripcion.GetProperty("descripcion").GetString());
        }
        finally
        {
            await ModificarAsync(admin, EstadosDeCliente.Activo, "Activo", PaletaDeEstados.Activo, descripcion);
        }

        await (await ModificarAsync(admin, EstadosDeCliente.Activo, "Visitado", PaletaDeEstados.Activo)).ProblemaAsync(400, "estado_protegido");
        await (await ModificarAsync(admin, EstadosDeCliente.DisponibleParaRevisita, "Disponible para re-visita", Azul))
            .ProblemaAsync(400, "estado_protegido");
        await (await admin.DeleteAsync($"/api/estados-cliente/{EstadosDeCliente.DisponibleParaRevisita}")).ProblemaAsync(400, "estado_protegido");
    }

    // Con minúscula, como la tesis y los diseños (tarea 26): hasta la migración NombreDisponibleParaRevisita decía "Re-visita".
    [Fact]
    public async Task El_catalogo_llama_al_estado_Disponible_para_re_visita_como_la_tesis()
    {
        using var promotor = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Promotor)).Token);

        var estados = (await (await promotor.GetAsync("/api/clientes/estados")).LeerJsonAsync()).EnumerateArray();

        var disponible = estados.Single(e => e.GetProperty("id").GetInt32() == EstadosDeCliente.DisponibleParaRevisita);
        Assert.Equal("Disponible para re-visita", disponible.GetProperty("nombre").GetString());
    }

    [Fact]
    public async Task Inactivo_cambia_su_color_y_su_descripcion_pero_no_su_nombre_ni_se_elimina()
    {
        using var admin = await api.AdminAsync();
        var inactivo = await EstadoAsync(admin, EstadosDeCliente.Inactivo);
        Assert.False(inactivo.GetProperty("esDelSistema").GetBoolean());
        Assert.True(inactivo.GetProperty("protegido").GetBoolean());

        try
        {
            var violeta = await (await ModificarAsync(admin, EstadosDeCliente.Inactivo, "Inactivo", Violeta)).LeerJsonAsync();
            Assert.Equal(Violeta, violeta.GetProperty("color").GetString());
        }
        finally
        {
            await ModificarAsync(admin, EstadosDeCliente.Inactivo, "Inactivo", PaletaDeEstados.Pizarra,
                inactivo.GetProperty("descripcion").GetString());
        }

        var problema = await (await ModificarAsync(admin, EstadosDeCliente.Inactivo, "De baja", PaletaDeEstados.Pizarra))
            .ProblemaAsync(400, "estado_protegido");
        Assert.StartsWith("Inactivo no cambia de nombre", problema.GetProperty("detail").GetString());
        await (await admin.DeleteAsync($"/api/estados-cliente/{EstadosDeCliente.Inactivo}")).ProblemaAsync(400, "estado_protegido");
    }

    [Fact]
    public async Task Un_estado_que_algun_cliente_tiene_no_se_elimina()
    {
        using var admin = await api.AdminAsync();
        using var operador = await api.OperadorAsync();
        var estadoId = (await RegistrarAsync(admin)).GetProperty("id").GetInt32();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());

        // Un estado nuevo se asigna a mano y saca al cliente del ciclo de visitas.
        var cliente = await (await operador.PutAsJsonAsync($"/api/clientes/{clienteId}/estado", new { estadoClienteId = estadoId }))
            .LeerJsonAsync();
        Assert.False(cliente.GetProperty("estado").GetProperty("delSistema").GetBoolean());
        Assert.Equal(1, (await EstadoAsync(admin, estadoId)).GetProperty("clientes").GetInt32());

        var problema = await (await admin.DeleteAsync($"/api/estados-cliente/{estadoId}")).ProblemaAsync(409, "estado_en_uso");
        Assert.Equal("Hay 1 cliente con este estado: asígnale otro antes de eliminarlo.", problema.GetProperty("detail").GetString());

        (await operador.PutAsJsonAsync($"/api/clientes/{clienteId}/estado", new { estadoClienteId = (int?)null })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/estados-cliente/{estadoId}")).StatusCode);
    }

    [Fact]
    public async Task Cada_operacion_pide_su_permiso()
    {
        using var admin = await api.AdminAsync();
        var id = (await RegistrarAsync(admin)).GetProperty("id").GetInt32();
        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);
        var (_, rol) = await admin.CrearRolAsync(Permisos.Parametros.ModificarEstadoCliente);
        using var soloModifica = api.ClienteCon((await api.CrearUsuarioAsync(rol)).Token);
        var cuerpo = new { nombre = Ayudantes.Unico("PAR"), color = Azul };

        await (await secretaria.GetAsync("/api/estados-cliente")).ProblemaAsync(403, "sin_permiso");
        await (await secretaria.PostAsJsonAsync("/api/estados-cliente", cuerpo)).ProblemaAsync(403, "sin_permiso");
        await (await secretaria.PutAsJsonAsync($"/api/estados-cliente/{id}", cuerpo)).ProblemaAsync(403, "sin_permiso");
        await (await secretaria.DeleteAsync($"/api/estados-cliente/{id}")).ProblemaAsync(403, "sin_permiso");
        // Los formularios siguen leyendo los estados sin estos permisos.
        Assert.Equal(HttpStatusCode.OK, (await secretaria.GetAsync("/api/clientes/estados")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await soloModifica.GetAsync("/api/estados-cliente")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await soloModifica.PutAsJsonAsync($"/api/estados-cliente/{id}", cuerpo)).StatusCode);
        await (await soloModifica.PostAsJsonAsync("/api/estados-cliente", cuerpo)).ProblemaAsync(403, "sin_permiso");
        await (await soloModifica.DeleteAsync($"/api/estados-cliente/{id}")).ProblemaAsync(403, "sin_permiso");
    }
}
