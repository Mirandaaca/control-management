using System.Security.Claims;

namespace ControlGestion.API.Seguridad;

/// <summary>
/// El usuario que hace la petición, leído del token. Los servicios lo usan en vez de leer HttpContext, así la
/// misma lógica sirve fuera de una petición HTTP.
/// </summary>
public interface IUsuarioActual
{
    string Id { get; }
    bool EsAdmin { get; }
    bool EsSupervisor { get; }
    bool EsPromotor { get; }

    /// <summary>Admin y Supervisor ven los registros de todos; cualquier otro rol, solo los suyos.</summary>
    bool VeTodosLosRegistros => EsAdmin || EsSupervisor;
}

public class UsuarioActual(IHttpContextAccessor http) : IUsuarioActual
{
    private ClaimsPrincipal Usuario =>
        http.HttpContext?.User ?? throw new InvalidOperationException("No hay una petición con un usuario autenticado.");

    public string Id => Usuario.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("El token no trae el id del usuario.");

    public bool EsAdmin => Usuario.IsInRole("Admin");
    public bool EsSupervisor => Usuario.IsInRole("Supervisor");
    public bool EsPromotor => Usuario.IsInRole("Promotor");
}
