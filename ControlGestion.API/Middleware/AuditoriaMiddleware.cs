using ControlGestion.API.Data;
using ControlGestion.API.Models;
using System.Security.Claims;

namespace ControlGestion.API.Middleware;

public class AuditoriaMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditoriaMiddleware> _logger;

    public AuditoriaMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory, ILogger<AuditoriaMiddleware> logger)
    {
        _next = next;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
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

        // DbContext propio: el de la petición puede tener cambios sin guardar o que ya fallaron
        // (por ejemplo, un conflicto de concurrencia), y guardarlos aquí los aplicaría o repetiría el error.
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditoriaLogs.Add(log);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // La respuesta ya se envió: un fallo de la bitácora no debe cortarla.
            _logger.LogError(ex, "No se pudo registrar en la bitácora {Accion} de {Modulo}.", accion, modulo);
        }
    }
}
