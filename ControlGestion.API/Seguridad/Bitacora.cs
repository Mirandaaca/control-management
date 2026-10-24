namespace ControlGestion.API.Seguridad;

/// <summary>
/// La acción con la que una operación queda en la bitácora, cuando no sale de su [Permiso]: los endpoints del propio
/// usuario (no tienen permiso) y los que piden dos. Sin módulo, el de un [Permiso] del endpoint o, si no tiene, el de
/// Usuario, Seguridad y Auditoría.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class BitacoraAttribute(string accion, string? modulo = null) : Attribute
{
    public string Accion { get; } = accion;
    public string? Modulo { get; } = modulo;
}

/// <summary>Una escritura que no es una operación del usuario y no va a la bitácora (7.3.35, ext. *a).</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SinBitacoraAttribute : Attribute;

/// <summary>
/// Lo que el inicio de sesión le cuenta a la bitácora: la petición no trae token, así que el usuario sale del correo. Un
/// intento fallido se guarda solo si el correo es de un usuario: así no quedan correos ajenos en la base, ni un bot llena
/// la bitácora desde internet.
/// </summary>
public sealed class RegistroDeBitacora
{
    public const string AccionInicioFallido = "Inicio de sesión fallido";

    public string? UsuarioDelInicio { get; private set; }
    public bool InicioFallido { get; private set; }

    public void IniciaSesion(string usuarioId) => UsuarioDelInicio = usuarioId;

    public void FallaElInicio(string usuarioId)
    {
        UsuarioDelInicio = usuarioId;
        InicioFallido = true;
    }
}

public static class OperacionesDeBitacora
{
    private static readonly Dictionary<string, Permisos.Definicion> PorClave =
        Permisos.Catalogo.ToDictionary(d => d.Clave);

    /// <summary>Los 8 módulos del catálogo, en su orden: son las opciones del filtro de la bitácora.</summary>
    public static readonly IReadOnlyList<string> Modulos = Permisos.Catalogo.Select(d => d.Modulo).Distinct().ToList();

    /// <summary>
    /// El módulo y el nombre de la operación de un endpoint, o null si no va a la bitácora o no se sabe qué operación
    /// es (la prueba de BitacoraTests revisa que no quede ninguna escritura así).
    /// </summary>
    public static (string Modulo, string Nombre)? De(Endpoint? endpoint)
    {
        if (endpoint is null || endpoint.Metadata.GetMetadata<SinBitacoraAttribute>() is not null) return null;

        var definicion = endpoint.Metadata.GetOrderedMetadata<PermisoAttribute>()
            .SelectMany(p => p.Claves)
            .Select(c => PorClave.GetValueOrDefault(c))
            .FirstOrDefault(d => d is not null);
        var propia = endpoint.Metadata.GetMetadata<BitacoraAttribute>();

        if (propia is not null)
            return (propia.Modulo ?? definicion?.Modulo ?? Permisos.Seguridad.Modulo, propia.Accion);
        return definicion is null ? null : (definicion.Modulo, definicion.Nombre);
    }
}
