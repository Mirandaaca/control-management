using ControlGestion.API.Data;
using ControlGestion.API.Models;
using System.Security.Claims;

namespace ControlGestion.API.Middleware;

public class AuditoriaMiddleware
{
    private readonly RequestDelegate _next;

    public AuditoriaMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        await _next(context);

        var method = context.Request.Method;
        if (method != "POST" && method != "PUT" && method != "DELETE") return;

        // Solo registra respuestas exitosas (2xx) o 400/404 de validación de negocio
        var status = context.Response.StatusCode;
        if (status == 401 || status == 403 || status == 500) return;

        var usuarioId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Anonimo";
        var segments = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];

        // segments[0]="api", segments[1]=modulo, segments[2+]=recurso
        var modulo = segments.ElementAtOrDefault(1) ?? "Desconocido";
        var subRuta = segments.Length > 2 ? "/" + string.Join("/", segments.Skip(2)) : "";
        var accion = $"{method}{subRuta}";

        var log = new AuditoriaLog
        {
            UsuarioId = usuarioId,
            Accion = accion,
            Modulo = modulo,
            DireccionIp = context.Connection.RemoteIpAddress?.ToString(),
            Fecha = DateTime.UtcNow
        };

        db.AuditoriaLogs.Add(log);
        await db.SaveChangesAsync();
    }
}
