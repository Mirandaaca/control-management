using System.Security.Claims;
using System.Threading.RateLimiting;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ControlGestion.API.Seguridad;

/// <summary>
/// Los límites de cada usuario para lo que se le pide al proveedor de rutas (tarea 25), para que nadie gaste solo su cupo
/// diario: un camino cada 10 s y 300 al día, y un orden de las visitas cada 20 s y 50 al día (parte C). Va después de la
/// autorización: un pedido sin permiso no gasta el límite. Vive en memoria, como el tope diario de
/// <see cref="CupoDeNavegacion"/>.
/// </summary>
public static class LimiteDeCaminos
{
    public const string Politica = "caminos";
    public const string PoliticaDeOrdenes = "ordenes";

    public static void Configurar(RateLimiterOptions opciones)
    {
        opciones.RejectionStatusCode = StatusCodes.Status503ServiceUnavailable;
        opciones.OnRejected = ResponderAsync;
        opciones.AddPolicy(Politica, http =>
        {
            var navegacion = http.RequestServices.GetRequiredService<IOptions<NavegacionOptions>>().Value;
            return PorUsuario(http, navegacion.SegundosEntreCaminos, navegacion.CaminosPorUsuarioAlDia);
        });
        opciones.AddPolicy(PoliticaDeOrdenes, http =>
        {
            var navegacion = http.RequestServices.GetRequiredService<IOptions<NavegacionOptions>>().Value;
            return PorUsuario(http, navegacion.SegundosEntreOrdenes, navegacion.OrdenesPorUsuarioAlDia);
        });
    }

    private static RateLimitPartition<string> PorUsuario(HttpContext http, int segundosEntrePedidos, int pedidosAlDia)
    {
        var usuario = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        return RateLimitPartition.Get(usuario, _ => RateLimiter.CreateChained(
            new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1,
                Window = TimeSpan.FromSeconds(segundosEntrePedidos),
                QueueLimit = 0
            }),
            new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = pedidosAlDia,
                Window = TimeSpan.FromDays(1),
                QueueLimit = 0
            })));
    }

    // Responde como cualquier otra falla del proveedor: la app se queda con lo que tenía y avisa.
    private static async ValueTask ResponderAsync(OnRejectedContext contexto, CancellationToken cancelacion)
    {
        var http = contexto.HttpContext;
        var deOrdenes = http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            == PoliticaDeOrdenes;
        http.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Servicio no disponible",
                Detail = deOrdenes ? NavegacionService.MensajeOrdenNoDisponible : NavegacionService.MensajeNoDisponible,
                Extensions = { ["codigo"] = deOrdenes ? NavegacionService.CodigoOrdenNoDisponible : NavegacionService.CodigoNoDisponible }
            }
        });
    }
}
