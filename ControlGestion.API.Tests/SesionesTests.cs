using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using ControlGestion.API.Data;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
}
