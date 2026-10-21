using System.Security.Claims;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ControlGestion.API.Seguridad;

/// <summary>
/// Revisa en la base, en cada petición con token, que su sesión siga abierta y el usuario siga activo, y carga los
/// permisos de su rol. Así cerrar sesión, la inactividad, cambiar la contraseña o desactivar a alguien aplican de
/// inmediato, y un cambio de permisos se ve sin volver a iniciar sesión: el token no lleva ni el rol ni los permisos.
/// </summary>
public static class SesionDesdeLaBase
{
    // UltimaActividad se escribe como mucho cada 5 minutos, para no escribir en la base en cada petición. Por eso la
    // inactividad que corta la sesión queda entre 7 h 55 min y 8 h.
    private static readonly TimeSpan CadaCuantoSeAnotaLaActividad = TimeSpan.FromMinutes(5);

    public static async Task CargarAsync(TokenValidatedContext contexto)
    {
        var servicios = contexto.HttpContext.RequestServices;
        var db = servicios.GetRequiredService<AppDbContext>();
        var ahora = servicios.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var inactividad = TimeSpan.FromHours(servicios.GetRequiredService<IOptions<SesionOptions>>().Value.HorasDeInactividad);

        var usuarioId = contexto.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        // Los tokens de antes de las sesiones en el servidor no traen la suya: dejan de servir.
        if (usuarioId == null || !Guid.TryParse(contexto.Principal!.FindFirstValue(ClaimsDeSeguridad.Sesion), out var sesionId))
        {
            contexto.Fail("El token no trae su sesión.");
            return;
        }

        var activaDesde = ahora - inactividad;
        var sesion = await db.Sesiones
            .Where(s => s.Id == sesionId && s.UsuarioId == usuarioId && s.CerradaEn == null
                && s.UltimaActividad > activaDesde && s.Usuario!.Activo)
            .Select(s => new
            {
                s.UltimaActividad,
                Permisos = db.UserRoles
                    .Where(ur => ur.UserId == s.UsuarioId)
                    .SelectMany(ur => db.RolesPermisos.Where(rp => rp.RolId == ur.RoleId).Select(rp => rp.Permiso!.Clave))
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (sesion == null)
        {
            contexto.Fail("La sesión está cerrada o vencida, o el usuario no existe o está inactivo.");
            return;
        }

        if (ahora - sesion.UltimaActividad >= CadaCuantoSeAnotaLaActividad)
        {
            // El filtro repite la condición: si llegan dos peticiones juntas, solo una escribe.
            var anotadaAntesDe = ahora - CadaCuantoSeAnotaLaActividad;
            await db.Sesiones
                .Where(s => s.Id == sesionId && s.UltimaActividad <= anotadaAntesDe)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.UltimaActividad, ahora));
        }

        var identidad = (ClaimsIdentity)contexto.Principal!.Identity!;
        identidad.AddClaims(sesion.Permisos.Select(clave => new Claim(ClaimsDeSeguridad.Permiso, clave)));
    }

    /// <summary>
    /// Un token que llegó y no sirve, por la razón que sea (sesión cerrada o vencida, usuario inactivo, firma inválida o
    /// exp vencido), responde 401 sesion_terminada: el panel y la app vuelven al inicio de sesión con ese aviso. Sin
    /// token, la respuesta sigue siendo 401 no_autenticado (RespuestasDeError).
    /// </summary>
    public static async Task ResponderSesionTerminadaAsync(JwtBearerChallengeContext contexto)
    {
        if (contexto.AuthenticateFailure == null) return;

        contexto.HandleResponse();
        var http = contexto.HttpContext;
        http.Response.StatusCode = StatusCodes.Status401Unauthorized;
        http.Response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";
        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Sesión terminada",
                Detail = "Tu sesión terminó: vuelve a entrar.",
                Extensions = { ["codigo"] = "sesion_terminada" }
            }
        });
    }
}
