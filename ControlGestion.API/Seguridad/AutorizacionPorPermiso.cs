using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ControlGestion.API.Seguridad;

public static class ClaimsDeSeguridad
{
    public const string Permiso = "permiso";

    /// <summary>El id de la sesión. JwtBearer no traduce "sid" al leer el token: se escribe y se lee igual.</summary>
    public const string Sesion = JwtRegisteredClaimNames.Sid;
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
