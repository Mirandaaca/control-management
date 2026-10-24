using Microsoft.AspNetCore.Authorization;

namespace ControlGestion.API.Seguridad;

/// <summary>
/// Exige al menos uno de los permisos indicados: <c>[Permiso(Permisos.Cobranzas.RegistrarPago)]</c>. Sin ninguno, la API
/// responde 403. El nombre de la política lleva las claves; ProveedorDePoliticasDePermiso la arma a partir de él.
/// </summary>
public sealed class PermisoAttribute : AuthorizeAttribute
{
    public const string Prefijo = "permiso:";

    public PermisoAttribute(params string[] claves)
    {
        Claves = claves;
        Policy = Prefijo + string.Join(',', claves);
    }

    /// <summary>La bitácora toma de aquí el módulo y el nombre de la operación.</summary>
    public IReadOnlyList<string> Claves { get; }
}
