namespace ControlGestion.API.Excepciones;

/// <summary>
/// Error esperado del negocio. ManejadorDeExcepciones lo convierte en una respuesta ProblemDetails con su
/// código HTTP, un <see cref="Codigo"/> estable para el panel y la app, y el mensaje como "detail".
/// </summary>
public abstract class ExcepcionDeNegocio(int estadoHttp, string codigo, string titulo, string mensaje) : Exception(mensaje)
{
    public int EstadoHttp { get; } = estadoHttp;
    public string Codigo { get; } = codigo;
    public string Titulo { get; } = titulo;

    /// <summary>Campo → mensajes, si el error pertenece a campos del formulario: van en "errors", junto a su campo.</summary>
    public virtual IReadOnlyDictionary<string, string[]>? Errores => null;
}

public class NoEncontradoException(string mensaje)
    : ExcepcionDeNegocio(StatusCodes.Status404NotFound, "no_encontrado", "No encontrado", mensaje);

/// <summary>
/// Con errores, la regla además marca sus campos (el monto de un pago, la cantidad de cada línea sin stock): el panel
/// y la app los ponen junto a su campo sin perder el código propio de la regla.
/// </summary>
public class ReglaDeNegocioException(string codigo, string mensaje, IReadOnlyDictionary<string, string[]>? errores = null)
    : ExcepcionDeNegocio(StatusCodes.Status400BadRequest, codigo, "Regla de negocio", mensaje)
{
    public override IReadOnlyDictionary<string, string[]>? Errores { get; } = errores;
}

/// <summary>
/// Datos inválidos que solo el servicio puede revisar, porque dependen de la base (el precio de un producto). Responde
/// igual que un error de DataAnnotations: 400 "validacion", con el mensaje de cada campo en "errors".
/// </summary>
public class ValidacionException(IReadOnlyDictionary<string, string[]> errores)
    : ExcepcionDeNegocio(StatusCodes.Status400BadRequest, "validacion", "Datos inválidos", "Revisa los datos marcados.")
{
    /// <summary>Campo → mensajes. El campo va como en el JSON del cliente: "detalles[1].descuento".</summary>
    public override IReadOnlyDictionary<string, string[]> Errores { get; } = errores;
}

public class ConflictoException(string codigo, string mensaje)
    : ExcepcionDeNegocio(StatusCodes.Status409Conflict, codigo, "Conflicto", mensaje);

public class NoAutenticadoException(string codigo, string mensaje)
    : ExcepcionDeNegocio(StatusCodes.Status401Unauthorized, codigo, "No autenticado", mensaje);
