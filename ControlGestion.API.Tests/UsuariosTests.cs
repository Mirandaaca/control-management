using System.Net;
using System.Net.Http.Json;
using ControlGestion.API.Tests.Infraestructura;
using ControlGestion.API.Seguridad;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class UsuariosTests(ApiDePrueba api)
{
    private static object NuevoUsuario(string rol = Roles.Promotor) => new
    {
        email = $"{Ayudantes.Unico("u").ToLowerInvariant()}@test.local",
        nombreCompleto = "Usuario de prueba",
        password = "Clave12345",
        rol
    };

    [Fact]
    public async Task El_admin_crea_un_usuario_con_su_rol()
    {
        using var admin = await api.AdminAsync();
        var respuesta = await admin.PostAsJsonAsync("/api/usuarios", NuevoUsuario(Roles.GerenteGeneral));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var usuario = await respuesta.LeerJsonAsync();
        Assert.Equal(Roles.GerenteGeneral, usuario.GetProperty("rol").GetString());
        Assert.True(usuario.GetProperty("activo").GetBoolean());
    }

    [Fact]
    public async Task Un_rol_que_no_existe_responde_400()
    {
        using var admin = await api.AdminAsync();
        await (await admin.PostAsJsonAsync("/api/usuarios", NuevoUsuario("Root"))).ProblemaAsync(400, "rol_no_existe");
    }

    [Theory]
    [InlineData("abc1234")] // 7 caracteres
    [InlineData("abcdefgh")] // sin número
    public async Task La_contrasena_necesita_8_caracteres_y_un_numero(string contrasena)
    {
        using var admin = await api.AdminAsync();
        var usuario = new { email = $"{Ayudantes.Unico("u").ToLowerInvariant()}@test.local", nombreCompleto = "Prueba", password = contrasena, rol = Roles.Promotor };

        var problema = await (await admin.PostAsJsonAsync("/api/usuarios", usuario)).ProblemaAsync(400, "validacion");
        Assert.True(problema.GetProperty("errors").TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Un_correo_repetido_responde_409()
    {
        using var admin = await api.AdminAsync();
        var usuario = NuevoUsuario();
        (await admin.PostAsJsonAsync("/api/usuarios", usuario)).EnsureSuccessStatusCode();

        await (await admin.PostAsJsonAsync("/api/usuarios", usuario)).ProblemaAsync(409, "correo_duplicado");
    }

    [Fact]
    public async Task Un_promotor_no_puede_crear_usuarios()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var cliente = api.ClienteCon(promotor.Token);
        await (await cliente.PostAsJsonAsync("/api/usuarios", NuevoUsuario(Roles.Administrador))).ProblemaAsync(403, "sin_permiso");
    }

    [Fact]
    public async Task El_admin_edita_el_nombre_el_correo_y_el_rol()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var admin = await api.AdminAsync();
        var correoNuevo = $"{Ayudantes.Unico("nuevo").ToLowerInvariant()}@test.local";

        var respuesta = await admin.PutAsJsonAsync($"/api/usuarios/{promotor.Id}",
            new { email = correoNuevo, nombreCompleto = "Nombre editado", rol = Roles.Secretaria });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var usuario = await respuesta.LeerJsonAsync();
        Assert.Equal("Nombre editado", usuario.GetProperty("nombreCompleto").GetString());
        Assert.Equal(correoNuevo, usuario.GetProperty("email").GetString());
        Assert.Equal(Roles.Secretaria, usuario.GetProperty("rol").GetString());
        // El correo es también el usuario con el que se inicia sesión.
        Assert.False(string.IsNullOrEmpty(await api.IniciarSesionAsync(correoNuevo, promotor.Contrasena)));
    }

    [Fact]
    public async Task Editar_con_el_correo_de_otro_usuario_responde_409()
    {
        var uno = await api.CrearUsuarioAsync(Roles.Promotor);
        var otro = await api.CrearUsuarioAsync(Roles.Promotor);
        using var admin = await api.AdminAsync();

        await (await admin.PutAsJsonAsync($"/api/usuarios/{uno.Id}",
            new { email = otro.Correo, nombreCompleto = "Prueba", rol = Roles.Promotor })).ProblemaAsync(409, "correo_duplicado");
    }

    [Fact]
    public async Task Un_usuario_se_desactiva_y_se_vuelve_a_activar()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var admin = await api.AdminAsync();

        var desactivado = await (await admin.PutAsync($"/api/usuarios/{promotor.Id}/desactivar", null)).LeerJsonAsync();
        var activado = await (await admin.PutAsync($"/api/usuarios/{promotor.Id}/activar", null)).LeerJsonAsync();

        Assert.False(desactivado.GetProperty("activo").GetBoolean());
        Assert.True(activado.GetProperty("activo").GetBoolean());
    }

    [Fact]
    public async Task Los_usuarios_no_se_borran()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var admin = await api.AdminAsync();

        await (await admin.DeleteAsync($"/api/usuarios/{promotor.Id}")).ProblemaAsync(405, "metodo_no_permitido");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/usuarios/{promotor.Id}")).StatusCode);
    }

    [Fact]
    public async Task El_administrador_no_se_desactiva_ni_se_cambia_el_rol_a_si_mismo()
    {
        using var admin = await api.AdminAsync();
        var yo = (await (await admin.GetAsync("/api/usuarios")).LeerJsonAsync()).EnumerateArray()
            .Single(u => u.GetProperty("email").GetString() == ApiDePrueba.CorreoAdmin);
        var id = yo.GetProperty("id").GetString();

        await (await admin.PutAsync($"/api/usuarios/{id}/desactivar", null)).ProblemaAsync(400, "no_puede_desactivarse");
        await (await admin.PutAsJsonAsync($"/api/usuarios/{id}",
            new { email = ApiDePrueba.CorreoAdmin, nombreCompleto = "Administrador del Sistema", rol = Roles.GerenteGeneral }))
            .ProblemaAsync(400, "no_puede_cambiar_su_rol");
    }

    [Fact]
    public async Task Quien_asigna_zonas_o_pedidos_ve_los_promotores_activos_con_su_id_y_nombre()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        var gerente = await api.CrearUsuarioAsync(Roles.GerenteGeneral);
        using var cliente = api.ClienteCon(gerente.Token);

        var promotores = (await (await cliente.GetAsync("/api/usuarios/promotores")).LeerJsonAsync()).EnumerateArray().ToList();

        var encontrado = Assert.Single(promotores, p => p.GetProperty("id").GetString() == promotor.Id);
        Assert.Equal(["id", "nombreCompleto"], encontrado.EnumerateObject().Select(c => c.Name));
        Assert.DoesNotContain(promotores, p => p.GetProperty("id").GetString() == secretaria.Id);
        await (await cliente.GetAsync("/api/usuarios")).ProblemaAsync(403, "sin_permiso");
        // La secretaria también los ve (asigna pedidos); el promotor no tiene ninguno de los cuatro permisos.
        Assert.Equal(HttpStatusCode.OK, (await api.ClienteCon(secretaria.Token).GetAsync("/api/usuarios/promotores")).StatusCode);
        await (await api.ClienteCon(promotor.Token).GetAsync("/api/usuarios/promotores")).ProblemaAsync(403, "sin_permiso");
    }
}
