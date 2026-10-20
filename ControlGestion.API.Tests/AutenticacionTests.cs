using System.Net;
using System.Net.Http.Json;
using ControlGestion.API.Tests.Infraestructura;
using ControlGestion.API.Seguridad;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class AutenticacionTests(ApiDePrueba api)
{
    [Fact]
    public async Task Login_correcto_devuelve_token_y_roles()
    {
        using var cliente = api.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login",
            new { email = ApiDePrueba.CorreoAdmin, password = api.ContrasenaAdmin });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.False(string.IsNullOrEmpty(cuerpo.GetProperty("token").GetString()));
        Assert.Contains(Roles.Administrador, cuerpo.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task Login_con_contrasena_incorrecta_responde_401()
    {
        using var cliente = api.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login",
            new { email = ApiDePrueba.CorreoAdmin, password = "incorrecta1" });

        var problema = await respuesta.ProblemaAsync(401, "credenciales_incorrectas");
        Assert.Equal("Credenciales incorrectas.", problema.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task El_registro_anonimo_ya_no_existe()
    {
        var cuerpo = new { email = "anonimo@test.local", password = "abc12345", nombreCompleto = "Anónimo", rol = "Admin" };
        using var anonimo = api.CreateClient();
        using var admin = await api.AdminAsync();

        await (await anonimo.PostAsJsonAsync("/api/auth/register", cuerpo)).ProblemaAsync(401, "no_autenticado");
        await (await admin.PostAsJsonAsync("/api/auth/register", cuerpo)).ProblemaAsync(404, "no_encontrado");
    }

    [Fact]
    public async Task Sin_token_responde_401_con_ProblemDetails()
    {
        using var anonimo = api.CreateClient();
        await (await anonimo.GetAsync("/api/clientes")).ProblemaAsync(401, "no_autenticado");
    }

    [Fact]
    public async Task Un_rol_sin_permiso_responde_403()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var cliente = api.ClienteCon(promotor.Token);
        await (await cliente.GetAsync("/api/usuarios")).ProblemaAsync(403, "sin_permiso");
    }

    [Fact]
    public async Task El_token_de_un_usuario_desactivado_deja_de_servir()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var cliente = api.ClienteCon(promotor.Token);
        using var admin = await api.AdminAsync();
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/clientes")).StatusCode);

        (await admin.PutAsync($"/api/usuarios/{promotor.Id}/activar", null)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/clientes")).StatusCode);
    }

    [Fact]
    public async Task El_token_de_un_usuario_borrado_deja_de_servir()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var cliente = api.ClienteCon(promotor.Token);
        using var admin = await api.AdminAsync();
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/clientes")).StatusCode);

        (await admin.DeleteAsync($"/api/usuarios/{promotor.Id}")).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/clientes")).StatusCode);
    }
}
