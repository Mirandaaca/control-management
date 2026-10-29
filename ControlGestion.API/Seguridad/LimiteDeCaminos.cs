using System.Security.Claims;
using System.Threading.RateLimiting;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ControlGestion.API.Seguridad;

/// <summary>
/// El límite de caminos de cada usuario (tarea 25): uno cada 10 s y 300 al día, para que nadie gaste solo el cupo diario
/// del proveedor. Va después de la autorización: un pedido sin permiso no gasta el límite. Vive en memoria, como el tope
/// diario de <see cref="CupoDeNavegacion"/>.
/// </summary>
public static class LimiteDeCaminos
{
    public const string Politica = "caminos";

    public static void Configurar(RateLimiterOptions opciones)
    {
        opciones.RejectionStatusCode = StatusCodes.Status503ServiceUnavailable;
        opciones.OnRejected = ResponderAsync;
        opciones.AddPolicy(Politica, http =>
        {
            var navegacion = http.RequestServices.GetRequiredService<IOptions<NavegacionOptions>>().Value;
            var usuario = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            return RateLimitPartition.Get(usuario, _ => RateLimiter.CreateChained(
                new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 1,
                    Window = TimeSpan.FromSeconds(navegacion.SegundosEntreCaminos),
                    QueueLimit = 0
                }),
                new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
                {
                    PermitLimit = navegacion.CaminosPorUsuarioAlDia,
                    Window = TimeSpan.FromDays(1),
                    QueueLimit = 0
                })));
        });
    }

    // Responde como cualquier otra falla del camino: la app deja el camino anterior y avisa.
    private static async ValueTask ResponderAsync(OnRejectedContext contexto, CancellationToken cancelacion)
    {
        var http = contexto.HttpContext;
        http.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Servicio no disponible",
                Detail = NavegacionService.MensajeNoDisponible,
                Extensions = { ["codigo"] = NavegacionService.CodigoNoDisponible }
            }
        });
    }
}
