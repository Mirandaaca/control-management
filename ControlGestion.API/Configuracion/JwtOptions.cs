using System.Text;
using Microsoft.Extensions.Options;

namespace ControlGestion.API.Configuracion;

/// <summary>
/// Configuración del token JWT, leída de la sección "Jwt".
/// Issuer y Audience vienen de appsettings.json. Key es secreta: viene de user-secrets
/// en desarrollo o de la variable de entorno Jwt__Key en producción.
/// </summary>
public class JwtOptions
{
    public const string Seccion = "Jwt";

    // HMAC-SHA256 necesita una clave de al menos 256 bits (32 bytes).
    public const int BytesMinimosClave = 32;

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}

/// <summary>
/// Se ejecuta la primera vez que alguien pide las JwtOptions. Si devuelve errores,
/// se lanza OptionsValidationException y la API no arranca.
/// </summary>
public class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions opciones)
    {
        var errores = new List<string>();

        if (string.IsNullOrWhiteSpace(opciones.Key))
        {
            errores.Add(ConfiguracionFaltante.Mensaje("Jwt:Key",
                $"<clave aleatoria de al menos {JwtOptions.BytesMinimosClave} bytes>"));
        }
        else
        {
            var bytes = Encoding.UTF8.GetByteCount(opciones.Key);
            if (bytes < JwtOptions.BytesMinimosClave)
                errores.Add(
                    $"La clave 'Jwt:Key' mide {bytes} bytes y debe medir al menos {JwtOptions.BytesMinimosClave}. " +
                    "Genera una más larga y guárdala con: dotnet user-secrets set \"Jwt:Key\" \"<clave nueva>\" " +
                    "(en producción, en la variable de entorno Jwt__Key). " +
                    "Si existe la variable de entorno Jwt__Key, tiene prioridad sobre user-secrets.");
        }

        if (string.IsNullOrWhiteSpace(opciones.Issuer))
            errores.Add("Falta la configuración 'Jwt:Issuer' en appsettings.json.");
        if (string.IsNullOrWhiteSpace(opciones.Audience))
            errores.Add("Falta la configuración 'Jwt:Audience' en appsettings.json.");

        return errores.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errores);
    }
}
