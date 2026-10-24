using ControlGestion.API.Data;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using System.Security.Claims;

namespace ControlGestion.API.Middleware;

/// <summary>
/// La bitácora (8.5, 7.3.35 ext. *a): cada operación que registra, modifica o anula datos y salió bien, con su usuario,
/// su módulo, su acción y la IP. Un 400 o un 409 no cambió nada y no se guarda. Aparte, el inicio y el cierre de sesión,
/// y el inicio fallido de un usuario que existe.
/// </summary>
public class AuditoriaMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditoriaMiddleware> _logger;
    private readonly TimeProvider _reloj;

    public AuditoriaMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory, ILogger<AuditoriaMiddleware> logger,
        TimeProvider reloj)
    {
        _next = next;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _reloj = reloj;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // El manejador de excepciones borra el endpoint antes de responder un error: se lee aquí, al volver con éxito.
        await _next(context);

        var registro = context.RequestServices.GetService<RegistroDeBitacora>();
        var log = registro is { InicioFallido: true, UsuarioDelInicio: { } fallido }
            ? Nuevo(context, fallido, Permisos.Seguridad.Modulo, RegistroDeBitacora.AccionInicioFallido)
            : DeLaOperacion(context, registro);
        if (log is null) return;

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
            _logger.LogError(ex, "No se pudo registrar en la bitácora {Accion} de {Modulo}.", log.Accion, log.Modulo);
        }
    }

    private AuditoriaLog? DeLaOperacion(HttpContext context, RegistroDeBitacora? registro)
    {
        var metodo = context.Request.Method;
        if (metodo != HttpMethods.Post && metodo != HttpMethods.Put && metodo != HttpMethods.Delete) return null;
        if (context.Response.StatusCode is < 200 or >= 300) return null;
        if (OperacionesDeBitacora.De(context.GetEndpoint()) is not { } operacion) return null;

        // El inicio de sesión no trae token: el usuario lo deja SesionService.
        var usuarioId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? registro?.UsuarioDelInicio;
        if (usuarioId is null) return null;

        return Nuevo(context, usuarioId, operacion.Modulo, $"{operacion.Nombre} ({metodo} {context.Request.Path})");
    }

    private AuditoriaLog Nuevo(HttpContext context, string usuarioId, string modulo, string accion) => new()
    {
        UsuarioId = usuarioId,
        Modulo = modulo,
        Accion = accion,
        DireccionIp = context.Connection.RemoteIpAddress?.ToString(),
        Fecha = _reloj.GetUtcNow().UtcDateTime
    };
}
