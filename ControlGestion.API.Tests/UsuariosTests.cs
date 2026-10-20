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
        Assert.Equal([Roles.GerenteGeneral], usuario.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.True(usuario.GetProperty("activo").GetBoolean());
    }

    [Fact]
    public async Task Un_rol_que_no_existe_responde_400()
    {
        using var admin = await api.AdminAsync();
        var problema = await (await admin.PostAsJsonAsync("/api/usuarios", NuevoUsuario("Root"))).ProblemaAsync(400, "validacion");
        Assert.True(problema.GetProperty("errors").TryGetProperty("rol", out _));
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
    public async Task El_gerente_lista_los_activos_pero_no_administra_usuarios()
    {
        var gerente = await api.CrearUsuarioAsync(Roles.GerenteGeneral);
        using var cliente = api.ClienteCon(gerente.Token);

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/usuarios/activos")).StatusCode);
        await (await cliente.GetAsync("/api/usuarios")).ProblemaAsync(403, "sin_permiso");
    }
}
