namespace ControlGestion.API.Configuracion;

/// <summary>
/// Arma el mensaje de error cuando falta una clave de configuración obligatoria.
/// Nunca incluye valores: solo el nombre de la clave y cómo configurarla.
/// </summary>
public static class ConfiguracionFaltante
{
    public static string Mensaje(string clave, string ejemplo) =>
        $"Falta la configuración '{clave}'. " +
        $"En desarrollo, guárdala desde la carpeta ControlGestion.API con: dotnet user-secrets set \"{clave}\" \"{ejemplo}\". " +
        $"En producción, usa la variable de entorno {VariableDeEntorno(clave)}.";

    // Una clave anidada como "Jwt:Key" se escribe "Jwt__Key" en una variable de entorno.
    public static string VariableDeEntorno(string clave) => clave.Replace(":", "__");
}
