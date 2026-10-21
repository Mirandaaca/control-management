using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class RolesTests(ApiDePrueba api)
{
    private static async Task<JsonElement> RolAdministradorAsync(HttpClient admin) =>
        (await (await admin.GetAsync("/api/roles")).LeerJsonAsync()).EnumerateArray()
            .Single(r => r.GetProperty("nombre").GetString() == Roles.Administrador);

    private static List<string> Claves(JsonElement rol) =>
        rol.GetProperty("permisos").EnumerateArray().Select(p => p.GetString()!).ToList();

    [Fact]
    public async Task El_admin_crea_un_rol_con_sus_permisos()
    {
        using var admin = await api.AdminAsync();
        var nombre = Ayudantes.Unico("Supervisor de zona");

        var respuesta = await admin.PostAsJsonAsync("/api/roles",
            new { nombre, permisos = new[] { Permisos.Territorial.SeguirRuta, Permisos.Territorial.VerMapaClientes } });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var rol = await respuesta.LeerJsonAsync();
        Assert.Equal(nombre, rol.GetProperty("nombre").GetString());
        Assert.Equal([Permisos.Territorial.SeguirRuta, Permisos.Territorial.VerMapaClientes], Claves(rol));
        Assert.False(rol.GetProperty("protegido").GetBoolean());
    }

    [Fact]
    public async Task El_catalogo_trae_los_66_permisos_con_su_modulo()
    {
        using var admin = await api.AdminAsync();
        var catalogo = (await (await admin.GetAsync("/api/roles/permisos")).LeerJsonAsync()).EnumerateArray().ToList();

        Assert.Equal(66, catalogo.Count);
        var registrarPago = catalogo.Single(p => p.GetProperty("clave").GetString() == Permisos.Cobranzas.RegistrarPago);
        Assert.Equal(Permisos.Cobranzas.Modulo, registrarPago.GetProperty("modulo").GetString());
        Assert.Equal("Registrar pagos", registrarPago.GetProperty("nombre").GetString());
    }

    [Fact]
    public async Task Un_rol_tiene_al_menos_un_permiso()
    {
        using var admin = await api.AdminAsync();
        var problema = await (await admin.PostAsJsonAsync("/api/roles", new { nombre = Ayudantes.Unico("Vacio"), permisos = Array.Empty<string>() }))
            .ProblemaAsync(400, "validacion");
        Assert.True(problema.GetProperty("errors").TryGetProperty("permisos", out _));
    }

    [Fact]
    public async Task Un_permiso_que_no_existe_o_un_nombre_repetido_se_rechazan()
    {
        using var admin = await api.AdminAsync();
        await (await admin.PostAsJsonAsync("/api/roles", new { nombre = Ayudantes.Unico("Rol"), permisos = new[] { "ventas.volar" } }))
            .ProblemaAsync(400, "permiso_no_existe");
        await (await admin.PostAsJsonAsync("/api/roles", new { nombre = Roles.Promotor, permisos = new[] { Permisos.Ventas.ConsultarHp } }))
            .ProblemaAsync(409, "rol_duplicado");
    }

    [Fact]
    public async Task El_rol_administrador_no_se_elimina_ni_se_renombra_ni_pierde_los_permisos_de_usuarios_y_roles()
    {
        using var admin = await api.AdminAsync();
        var rol = await RolAdministradorAsync(admin);
        var id = rol.GetProperty("id").GetString();
        var sinCrearRoles = Claves(rol).Where(c => c != Permisos.Seguridad.CrearRol).ToArray();

        Assert.True(rol.GetProperty("protegido").GetBoolean());
        await (await admin.DeleteAsync($"/api/roles/{id}")).ProblemaAsync(400, "rol_protegido");
        await (await admin.PutAsJsonAsync($"/api/roles/{id}", new { nombre = "Jefe", permisos = Claves(rol) }))
            .ProblemaAsync(400, "rol_protegido");
        await (await admin.PutAsJsonAsync($"/api/roles/{id}", new { nombre = Roles.Administrador, permisos = sinCrearRoles }))
            .ProblemaAsync(400, "permisos_protegidos");
        Assert.Contains(Permisos.Seguridad.CrearRol, Claves(await RolAdministradorAsync(admin)));
    }

    [Fact]
    public async Task Un_rol_con_usuarios_no_se_elimina_hasta_que_nadie_lo_tenga()
    {
        using var admin = await api.AdminAsync();
        var (rolId, rolNombre) = await admin.CrearRolAsync(Permisos.Ventas.ConsultarHp);
        var usuario = await api.CrearUsuarioAsync(rolNombre);

        await (await admin.DeleteAsync($"/api/roles/{rolId}")).ProblemaAsync(409, "rol_con_usuarios");

        (await admin.PutAsJsonAsync($"/api/usuarios/{usuario.Id}",
            new { email = usuario.Correo, nombreCompleto = "Prueba", rol = Roles.Promotor })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/roles/{rolId}")).StatusCode);
    }

    [Fact]
    public async Task Un_cambio_de_permisos_aplica_sin_volver_a_iniciar_sesion()
    {
        using var admin = await api.AdminAsync();
        var (rolId, rolNombre) = await admin.CrearRolAsync(Permisos.Ventas.ConsultarHp);
        var usuario = await api.CrearUsuarioAsync(rolNombre);
        using var cliente = api.ClienteCon(usuario.Token);
        var cambiarA = (string permiso) => admin.PutAsJsonAsync($"/api/roles/{rolId}", new { nombre = rolNombre, permisos = new[] { permiso } });

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/hojas-pedido")).StatusCode);

        (await cambiarA(Permisos.Ventas.ConsultarProductos)).EnsureSuccessStatusCode();
        await (await cliente.GetAsync("/api/hojas-pedido")).ProblemaAsync(403, "sin_permiso");

        (await cambiarA(Permisos.Ventas.ConsultarHp)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/hojas-pedido")).StatusCode);
    }

    [Fact]
    public async Task Sin_ver_la_cartera_completa_cada_usuario_ve_solo_su_cartera()
    {
        using var admin = await api.AdminAsync();
        using var operador = await api.OperadorAsync();
        var (rolId, rolNombre) = await admin.CrearRolAsync(Permisos.Cobranzas.ConsultarDeudas);
        var usuario = await api.CrearUsuarioAsync(rolNombre);
        var zonaId = await operador.CrearZonaAsync();
        var productoId = await operador.CrearProductoAsync(10m);
        var suya = await operador.CrearCobranzaAsync(await operador.CrearClienteAsync(await operador.CrearZonaAsync(usuario.Id)), zonaId, productoId, 1);
        var ajena = await operador.CrearCobranzaAsync(await operador.CrearClienteAsync(zonaId), zonaId, productoId, 1);
        using var cliente = api.ClienteCon(usuario.Token);
        var visibles = async () => (await (await cliente.GetAsync("/api/cobranzas")).LeerJsonAsync())
            .EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).ToList();

        var antes = await visibles();
        Assert.Contains(suya, antes);
        Assert.DoesNotContain(ajena, antes);

        (await admin.PutAsJsonAsync($"/api/roles/{rolId}", new
        {
            nombre = rolNombre,
            permisos = new[] { Permisos.Cobranzas.ConsultarDeudas, Permisos.Seguridad.VerCarteraCompleta }
        })).EnsureSuccessStatusCode();
        Assert.Contains(ajena, await visibles());
    }
}
