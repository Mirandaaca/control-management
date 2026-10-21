using Microsoft.Extensions.Options;

namespace ControlGestion.API.Configuracion;

/// <summary>Cuánto dura una sesión (sección "Sesion" de appsettings.json). No son secretos.</summary>
public class SesionOptions
{
    public const string Seccion = "Sesion";

    /// <summary>Sin ninguna petición durante estas horas, la sesión termina.</summary>
    public int HorasDeInactividad { get; set; } = 8;

    /// <summary>El vencimiento (exp) del token: un tope largo, porque lo que manda es la sesión guardada en la base.</summary>
    public int DiasDelToken { get; set; } = 7;
}

/// <summary>Detiene la API al arrancar si una duración no es mayor que cero.</summary>
public class SesionOptionsValidator : IValidateOptions<SesionOptions>
{
    public ValidateOptionsResult Validate(string? name, SesionOptions opciones)
    {
        var errores = new List<string>();
        if (opciones.HorasDeInactividad <= 0)
            errores.Add("'Sesion:HorasDeInactividad' debe ser mayor que cero.");
        if (opciones.DiasDelToken <= 0)
            errores.Add("'Sesion:DiasDelToken' debe ser mayor que cero.");
        return errores.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errores);
    }
}
