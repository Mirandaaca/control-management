using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace ControlGestion.API.Excepciones;

/// <summary>
/// Respuestas de error que no nacen de una excepción: los 401/403/404/405 que arma el pipeline y los 400 de validación.
/// </summary>
public static partial class RespuestasDeError
{
    public static void Personalizar(ProblemDetailsContext contexto)
    {
        var problema = contexto.ProblemDetails;
        problema.Instance ??= contexto.HttpContext.Request.Path;
        if (problema.Extensions.ContainsKey("codigo")) return;

        var (codigo, titulo, detalle) = problema.Status switch
        {
            400 => ("solicitud_invalida", "Datos inválidos", "La solicitud no es válida."),
            401 => ("no_autenticado", "No autenticado", "Inicia sesión para continuar."),
            403 => ("sin_permiso", "Sin permiso", "No tienes permiso para esta acción."),
            404 => ("no_encontrado", "No encontrado", "El recurso no existe."),
            405 => ("metodo_no_permitido", "Método no permitido", "Esta operación no está disponible en esta ruta."),
            415 => ("formato_no_soportado", "Formato no soportado", "El cuerpo debe enviarse en JSON."),
            _ => ("error", "Error", "No se pudo completar la solicitud.")
        };
        problema.Extensions["codigo"] = codigo;
        problema.Title = titulo;
        problema.Detail ??= detalle;
    }

    public static IActionResult Validacion(ActionContext contexto)
    {
        var parametrosDelCuerpo = contexto.ActionDescriptor.Parameters
            .Where(p => p.BindingInfo?.BindingSource == BindingSource.Body)
            .Select(p => p.Name)
            .ToHashSet();
        var hayErroresDeJson = contexto.ModelState.Keys.Any(k => k.StartsWith('$'));

        var errores = contexto.ModelState
            .Where(e => e.Value is { Errors.Count: > 0 })
            // Si el JSON no se pudo leer, ASP.NET agrega además "el campo dto es obligatorio", que no le dice nada al usuario.
            .Where(e => !(hayErroresDeJson && parametrosDelCuerpo.Contains(e.Key)))
            .GroupBy(e => NombreDeCampo(e.Key))
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(e => e.Value!.Errors.Select(error => Traducir(error, g.Key))).Distinct().ToArray());

        var problema = new ValidationProblemDetails(errores)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Datos inválidos",
            Detail = "Revisa los datos marcados.",
            Instance = contexto.HttpContext.Request.Path
        };
        problema.Extensions["codigo"] = "validacion";
        problema.Extensions["traceId"] = Activity.Current?.Id ?? contexto.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(problema) { ContentTypes = { "application/problem+json" } };
    }

    public static void TraducirMensajesDeEnlace(DefaultModelBindingMessageProvider m)
    {
        m.SetAttemptedValueIsInvalidAccessor((valor, campo) => $"El valor '{valor}' no es válido para '{campo}'.");
        m.SetMissingBindRequiredValueAccessor(campo => $"Falta el valor de '{campo}'.");
        m.SetMissingKeyOrValueAccessor(() => "Falta un valor.");
        m.SetMissingRequestBodyRequiredValueAccessor(() => "El cuerpo de la solicitud es obligatorio.");
        m.SetNonPropertyAttemptedValueIsInvalidAccessor(valor => $"El valor '{valor}' no es válido.");
        m.SetNonPropertyUnknownValueIsInvalidAccessor(() => "El valor no es válido.");
        m.SetNonPropertyValueMustBeANumberAccessor(() => "El valor debe ser un número.");
        m.SetUnknownValueIsInvalidAccessor(campo => $"El valor de '{campo}' no es válido.");
        m.SetValueIsInvalidAccessor(valor => $"El valor '{valor}' no es válido.");
        m.SetValueMustBeANumberAccessor(campo => $"'{campo}' debe ser un número.");
        m.SetValueMustNotBeNullAccessor(campo => $"'{campo}' no puede estar vacío.");
    }

    // "$.detalles[0].Cantidad" -> "detalles[0].cantidad": el mismo nombre que usa el JSON que envía el cliente.
    private static string NombreDeCampo(string clave)
    {
        var limpia = clave.TrimStart('$').TrimStart('.');
        return limpia.Length == 0
            ? "cuerpo"
            : string.Join('.', limpia.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
    }

    // Los atributos sin ErrorMessage y el lector de JSON escriben en inglés y con detalles internos.
    private static string Traducir(ModelError error, string campo)
    {
        var mensaje = error.ErrorMessage;
        if (error.Exception != null || string.IsNullOrEmpty(mensaje) || mensaje.Contains("Path: $"))
            return $"El valor de '{campo}' no es válido.";
        if (mensaje == "A non-empty request body is required.") return "El cuerpo de la solicitud es obligatorio.";
        if (Obligatorio().IsMatch(mensaje)) return $"El campo '{campo}' es obligatorio.";
        if (FueraDeRango().IsMatch(mensaje)) return $"El valor de '{campo}' está fuera del rango permitido.";
        if (LongitudMinima().IsMatch(mensaje)) return $"'{campo}' está vacío o es demasiado corto.";
        if (LongitudMaxima().IsMatch(mensaje)) return $"'{campo}' es demasiado largo.";
        if (mensaje.StartsWith("The ")) return $"El valor de '{campo}' no es válido.";
        return mensaje;
    }

    [GeneratedRegex(@"^The .+ field is required\.$")]
    private static partial Regex Obligatorio();

    [GeneratedRegex(@"^The field .+ must be between .+ and .+\.$")]
    private static partial Regex FueraDeRango();

    [GeneratedRegex(@"^The field .+ must be a string or array type with a minimum length")]
    private static partial Regex LongitudMinima();

    [GeneratedRegex(@"^The field .+ must be a string (or array type )?with a maximum length")]
    private static partial Regex LongitudMaxima();
}
