using System.Security.Claims;

namespace ControlGestion.API.Seguridad;

/// <summary>
/// El usuario que hace la petición, leído del token. Los servicios lo usan en vez de leer HttpContext, así la
/// misma lógica sirve fuera de una petición HTTP.
/// </summary>
public interface IUsuarioActual
{
    string Id { get; }

    /// <summary>Si el rol del usuario tiene el permiso. Los permisos se cargan de la base en cada petición.</summary>
    bool Tiene(string permiso);

    /// <summary>Sin este permiso, el usuario solo ve los registros de su cartera (7.3.31, extensión *b).</summary>
    bool VeCarteraCompleta => Tiene(Permisos.Seguridad.VerCarteraCompleta);
}

public class UsuarioActual(IHttpContextAccessor http) : IUsuarioActual
{
    private ClaimsPrincipal Usuario =>
        http.HttpContext?.User ?? throw new InvalidOperationException("No hay una petición con un usuario autenticado.");

    public string Id => Usuario.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("El token no trae el id del usuario.");

    public bool Tiene(string permiso) => Usuario.HasClaim(ClaimsDeSeguridad.Permiso, permiso);
}
