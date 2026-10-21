using System.Security.Claims;
using ControlGestion.API.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ControlGestion.API.Seguridad;

public static class ClaimsDeSeguridad
{
    public const string Permiso = "permiso";
}

public sealed class PermisoRequerido(IReadOnlyCollection<string> claves) : IAuthorizationRequirement
{
    public IReadOnlyCollection<string> Claves { get; } = claves;
}

/// <summary>Arma la política de cada [Permiso]; las demás (la de respaldo, por ejemplo) quedan como siempre.</summary>
public sealed class ProveedorDePoliticasDePermiso(IOptions<AuthorizationOptions> opciones) : DefaultAuthorizationPolicyProvider(opciones)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string nombre)
    {
        if (!nombre.StartsWith(PermisoAttribute.Prefijo, StringComparison.Ordinal))
            return base.GetPolicyAsync(nombre);

        var claves = nombre[PermisoAttribute.Prefijo.Length..].Split(',', StringSplitOptions.RemoveEmptyEntries);
        var politica = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermisoRequerido(claves))
            .Build();
        return Task.FromResult<AuthorizationPolicy?>(politica);
    }
}

public sealed class PermisoRequeridoHandler : AuthorizationHandler<PermisoRequerido>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext contexto, PermisoRequerido requisito)
    {
        if (requisito.Claves.Any(clave => contexto.User.HasClaim(ClaimsDeSeguridad.Permiso, clave)))
            contexto.Succeed(requisito);
        return Task.CompletedTask;
    }
}

public static class SesionDesdeLaBase
{
    /// <summary>
    /// Se ejecuta en cada petición con token. Una sola consulta comprueba que el usuario siga activo y trae los permisos
    /// de su rol: así desactivar a alguien o cambiar los permisos de un rol aplica de inmediato, sin volver a iniciar
    /// sesión (el token dura 8 h y no lleva ni el rol ni los permisos).
    /// </summary>
    public static async Task CargarAsync(TokenValidatedContext contexto)
    {
        var usuarioId = contexto.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var db = contexto.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

        var permisos = usuarioId == null ? null : await db.Users
            .Where(u => u.Id == usuarioId && u.Activo)
            .Select(u => db.UserRoles
                .Where(ur => ur.UserId == u.Id)
                .SelectMany(ur => db.RolesPermisos.Where(rp => rp.RolId == ur.RoleId).Select(rp => rp.Permiso!.Clave))
                .ToList())
            .FirstOrDefaultAsync();

        if (permisos == null)
        {
            contexto.Fail("El usuario no existe o está inactivo.");
            return;
        }

        var identidad = (ClaimsIdentity)contexto.Principal!.Identity!;
        identidad.AddClaims(permisos.Select(clave => new Claim(ClaimsDeSeguridad.Permiso, clave)));
    }
}
