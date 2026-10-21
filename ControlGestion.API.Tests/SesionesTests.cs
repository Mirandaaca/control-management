using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class SesionesTests(ApiDePrueba api)
{
    private static Guid SesionDelToken(string token) =>
        Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Single(c => c.Type == "sid").Value);

    private async Task<Sesion> LeerSesionAsync(Guid id)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Sesiones.AsNoTracking().SingleAsync(s => s.Id == id);
    }

    [Fact]
    public async Task El_login_guarda_una_sesion_abierta_con_su_origen()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);

        var token = await api.IniciarSesionAsync(promotor.Correo, promotor.Contrasena, Ayudantes.OrigenApp);

        var sesion = await LeerSesionAsync(SesionDelToken(token));
        Assert.Equal(promotor.Id, sesion.UsuarioId);
        Assert.Equal(OrigenSesion.App, sesion.Origen);
        Assert.Null(sesion.CerradaEn);
        Assert.Equal(sesion.Creada, sesion.UltimaActividad);
    }

    [Fact]
    public async Task El_login_sin_origen_responde_400_en_el_campo()
    {
        using var anonimo = api.CreateClient();
        var problema = await (await anonimo.PostAsJsonAsync("/api/auth/login",
            new { email = ApiDePrueba.CorreoAdmin, password = api.ContrasenaAdmin })).ProblemaAsync(400, "validacion");

        Assert.True(problema.GetProperty("errors").TryGetProperty("origen", out _));
    }

    [Fact]
    public async Task El_token_vence_en_7_dias_porque_lo_que_manda_es_la_sesion()
    {
        var antes = DateTime.UtcNow;
        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            await api.IniciarSesionAsync(ApiDePrueba.CorreoAdmin, api.ContrasenaAdmin));

        Assert.InRange(token.ValidTo, antes.AddDays(7).AddMinutes(-1), DateTime.UtcNow.AddDays(7).AddMinutes(1));
    }

    [Fact]
    public async Task El_token_sirve_hasta_8_horas_sin_actividad_y_despues_no()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var reloj = new RelojAjustable();
        await using var fabrica = api.ConReloj(reloj);
        using var cliente = fabrica.ClienteCon(await fabrica.IniciarSesionAsync(promotor.Correo, promotor.Contrasena));

        reloj.Adelantar(new TimeSpan(7, 59, 0));
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/auth/yo")).StatusCode);

        // Esa petición corrió el límite: las 8 horas se cuentan desde ella.
        reloj.Adelantar(new TimeSpan(7, 59, 0));
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/auth/yo")).StatusCode);

        reloj.Adelantar(new TimeSpan(8, 1, 0));
        var problema = await (await cliente.GetAsync("/api/auth/yo")).ProblemaAsync(401, "sesion_terminada");
        Assert.Equal("Tu sesión terminó: vuelve a entrar.", problema.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task La_actividad_se_anota_como_mucho_cada_5_minutos()
    {
        var reloj = new RelojAjustable();
        await using var fabrica = api.ConReloj(reloj);
        var token = await fabrica.IniciarSesionAsync(ApiDePrueba.CorreoAdmin, api.ContrasenaAdmin);
        using var cliente = fabrica.ClienteCon(token);
        var inicio = (await LeerSesionAsync(SesionDelToken(token))).UltimaActividad;

        reloj.Adelantar(TimeSpan.FromMinutes(4));
        (await cliente.GetAsync("/api/auth/yo")).EnsureSuccessStatusCode();
        Assert.Equal(inicio, (await LeerSesionAsync(SesionDelToken(token))).UltimaActividad);

        reloj.Adelantar(TimeSpan.FromMinutes(2));
        (await cliente.GetAsync("/api/auth/yo")).EnsureSuccessStatusCode();
        Assert.Equal(inicio.AddMinutes(6), (await LeerSesionAsync(SesionDelToken(token))).UltimaActividad,
            TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Cerrar_sesion_anula_el_token_aunque_alguien_lo_haya_copiado()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var cliente = api.ClienteCon(promotor.Token);
        using var copia = api.ClienteCon(promotor.Token);

        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PostAsync("/api/auth/logout", null)).StatusCode);

        await (await copia.GetAsync("/api/auth/yo")).ProblemaAsync(401, "sesion_terminada");
        Assert.NotNull((await LeerSesionAsync(SesionDelToken(promotor.Token))).CerradaEn);
    }

    [Fact]
    public async Task Cerrar_una_sesion_no_cierra_las_otras_del_mismo_usuario()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var enLaApp = api.ClienteCon(
            await api.IniciarSesionAsync(promotor.Correo, promotor.Contrasena, Ayudantes.OrigenApp));

        (await api.ClienteCon(promotor.Token).PostAsync("/api/auth/logout", null)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await enLaApp.GetAsync("/api/auth/yo")).StatusCode);
    }

    [Fact]
    public async Task Un_token_sin_sesion_o_invalido_responde_sesion_terminada()
    {
        // Así son los tokens de antes de las sesiones en el servidor: firmados con la misma clave, pero sin "sid".
        var jwt = api.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        using var admin = await api.AdminAsync();
        var adminId = (await (await admin.GetAsync("/api/auth/yo")).LeerJsonAsync()).GetProperty("id").GetString()!;
        var sinSesion = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            jwt.Issuer, jwt.Audience, [new Claim(ClaimTypes.NameIdentifier, adminId)],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)), SecurityAlgorithms.HmacSha256)));

        await (await api.ClienteCon(sinSesion).GetAsync("/api/auth/yo")).ProblemaAsync(401, "sesion_terminada");
        await (await api.ClienteCon("no-es-un-token").GetAsync("/api/auth/yo")).ProblemaAsync(401, "sesion_terminada");
    }
}
