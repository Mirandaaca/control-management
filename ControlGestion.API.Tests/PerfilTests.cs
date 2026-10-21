using System.Net;
using System.Net.Http.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class PerfilTests(ApiDePrueba api)
{
    private static string CorreoNuevo() => $"{Ayudantes.Unico("perfil").ToLowerInvariant()}@test.local";

    [Fact]
    public async Task Cada_usuario_ve_su_perfil_y_su_sesion()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var cliente = api.ClienteCon(promotor.Token);

        var perfil = await (await cliente.GetAsync("/api/perfil")).LeerJsonAsync();
        var sesion = await (await cliente.GetAsync("/api/auth/yo")).LeerJsonAsync();

        Assert.Equal(promotor.Correo, perfil.GetProperty("email").GetString());
        Assert.Equal(Roles.Promotor, perfil.GetProperty("rol").GetString());
        Assert.True(perfil.TryGetProperty("fechaCreacion", out _));
        Assert.Equal(promotor.Id, sesion.GetProperty("id").GetString());
        Assert.Equal(23, sesion.GetProperty("permisos").GetArrayLength());
    }

    [Fact]
    public async Task Cada_usuario_edita_su_nombre_y_su_correo()
    {
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        using var cliente = api.ClienteCon(secretaria.Token);
        var correo = CorreoNuevo();

        var respuesta = await cliente.PutAsJsonAsync("/api/perfil", new { email = correo, nombreCompleto = "  Nombre propio  " });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var perfil = await respuesta.LeerJsonAsync();
        Assert.Equal("Nombre propio", perfil.GetProperty("nombreCompleto").GetString());
        Assert.Equal(correo, perfil.GetProperty("email").GetString());
        Assert.False(string.IsNullOrEmpty(await api.IniciarSesionAsync(correo, secretaria.Contrasena)));
    }

    [Fact]
    public async Task El_perfil_no_acepta_el_correo_de_otro_usuario()
    {
        var uno = await api.CrearUsuarioAsync(Roles.Promotor);
        var otro = await api.CrearUsuarioAsync(Roles.Promotor);

        await (await api.ClienteCon(uno.Token).PutAsJsonAsync("/api/perfil", new { email = otro.Correo, nombreCompleto = "Prueba" }))
            .ProblemaAsync(409, "correo_duplicado");
    }

    [Fact]
    public async Task Cambiar_la_contrasena_pide_la_actual()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var cliente = api.ClienteCon(promotor.Token);

        await (await cliente.PutAsJsonAsync("/api/perfil/contrasena", new { actual = "NoEsLaMia1", nueva = "OtraClave99" }))
            .ProblemaAsync(400, "contrasena_actual_incorrecta");
    }

    [Theory]
    [InlineData("Corta12")] // 7 caracteres
    [InlineData("SinNumeros")]
    public async Task La_contrasena_nueva_necesita_8_caracteres_y_un_numero(string nueva)
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);

        var problema = await (await api.ClienteCon(promotor.Token).PutAsJsonAsync("/api/perfil/contrasena",
            new { actual = promotor.Contrasena, nueva })).ProblemaAsync(400, "validacion");
        Assert.True(problema.GetProperty("errors").TryGetProperty("nueva", out _));
    }

    [Fact]
    public async Task Con_la_contrasena_nueva_se_inicia_sesion_y_con_la_anterior_no()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        const string nueva = "ClaveNueva2026";

        var respuesta = await api.ClienteCon(promotor.Token).PutAsJsonAsync("/api/perfil/contrasena",
            new { actual = promotor.Contrasena, nueva });

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.False(string.IsNullOrEmpty(await api.IniciarSesionAsync(promotor.Correo, nueva)));
        using var anonimo = api.CreateClient();
        await (await anonimo.PostAsJsonAsync("/api/auth/login", new { email = promotor.Correo, password = promotor.Contrasena, origen = Ayudantes.OrigenPanel }))
            .ProblemaAsync(401, "credenciales_incorrectas");
    }
}
