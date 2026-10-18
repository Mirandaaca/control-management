using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace ControlGestion.API.Excepciones;

/// <summary>
/// Convierte toda excepción que llega al final del pipeline en ProblemDetails. Las de negocio llevan su propio
/// código; las de EF y MySQL que nacen de un dato inválido del usuario se traducen a 400 o 409 para que nunca
/// terminen en 500.
/// </summary>
public class ManejadorDeExcepciones(IProblemDetailsService problemDetails, ILogger<ManejadorDeExcepciones> logger)
    : IExceptionHandler
{
    private const string MensajeInterno = "Ocurrió un error inesperado. Si se repite, avisa a soporte con el código {0}.";

    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception excepcion, CancellationToken cancelacion)
    {
        var traceId = Activity.Current?.Id ?? http.TraceIdentifier;
        var (estado, codigo, titulo, detalle) = Traducir(excepcion, traceId);

        if (estado >= StatusCodes.Status500InternalServerError)
            logger.LogError(excepcion, "Error no controlado en {Metodo} {Ruta} ({TraceId}).",
                http.Request.Method, http.Request.Path, traceId);

        http.Response.StatusCode = estado;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = excepcion,
            ProblemDetails = new ProblemDetails
            {
                Status = estado,
                Title = titulo,
                Detail = detalle,
                Extensions = { ["codigo"] = codigo }
            }
        });
    }

    private static (int Estado, string Codigo, string Titulo, string Detalle) Traducir(Exception excepcion, string traceId) =>
        excepcion switch
        {
            ExcepcionDeNegocio e => (e.EstadoHttp, e.Codigo, e.Titulo, e.Message),
            DbUpdateConcurrencyException => (409, "conflicto_concurrencia", "Conflicto",
                "El registro cambió mientras lo editabas. Vuelve a intentarlo."),
            DbUpdateException { InnerException: MySqlException mysql } => TraducirMySql(mysql.Number, traceId),
            BadHttpRequestException e => (e.StatusCode, "solicitud_invalida", "Datos inválidos", "La solicitud no es válida."),
            _ => Interno(traceId)
        };

    // https://dev.mysql.com/doc/mysql-errors/8.4/en/server-error-reference.html
    private static (int, string, string, string) TraducirMySql(int numero, string traceId) => numero switch
    {
        1062 => (409, "duplicado", "Conflicto", "Ya existe un registro con esos datos."),
        1451 => (409, "tiene_registros_asociados", "Conflicto", "No se puede borrar porque tiene registros asociados."),
        1452 => (400, "referencia_invalida", "Datos inválidos", "Uno de los datos hace referencia a un registro que no existe."),
        1048 => (400, "dato_obligatorio", "Datos inválidos", "Falta un dato obligatorio."),
        1264 => (400, "valor_fuera_de_rango", "Datos inválidos", "Uno de los valores está fuera del rango permitido."),
        1406 => (400, "dato_demasiado_largo", "Datos inválidos", "Uno de los datos es demasiado largo."),
        _ => Interno(traceId)
    };

    private static (int, string, string, string) Interno(string traceId) =>
        (500, "error_interno", "Error interno", string.Format(MensajeInterno, traceId));
}
