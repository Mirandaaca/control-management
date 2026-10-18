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
}

public class NoEncontradoException(string mensaje)
    : ExcepcionDeNegocio(StatusCodes.Status404NotFound, "no_encontrado", "No encontrado", mensaje);

public class ReglaDeNegocioException(string codigo, string mensaje)
    : ExcepcionDeNegocio(StatusCodes.Status400BadRequest, codigo, "Regla de negocio", mensaje);

public class ConflictoException(string codigo, string mensaje)
    : ExcepcionDeNegocio(StatusCodes.Status409Conflict, codigo, "Conflicto", mensaje);

public class NoAutenticadoException(string codigo, string mensaje)
    : ExcepcionDeNegocio(StatusCodes.Status401Unauthorized, codigo, "No autenticado", mensaje);
