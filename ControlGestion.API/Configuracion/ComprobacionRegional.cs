using System.Globalization;

namespace ControlGestion.API.Configuracion;

/// <summary>
/// La API necesita dos cosas del sistema donde corre: la zona America/La_Paz (tzdata) y la cultura es-BO (ICU). Sin la
/// zona, cada fecha fallaría; sin ICU, .NET corre en modo invariante y es-BO no existe. Program.cs lo comprueba al
/// arrancar y, si falta algo, se detiene con este mensaje, en vez de responder 500 en la primera petición que lo use.
/// </summary>
public static class ComprobacionRegional
{
    public const string MontoEsperado = "Bs 1.234,50";

    /// <summary>Qué falta, o null si está todo. Los parámetros existen para probar el mensaje de cada caso.</summary>
    public static string? Problema(string zona = HoraBolivia.IdDeZona, string cultura = FormatoBolivia.IdDeCultura)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(zona);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return $"No existe la zona horaria '{zona}': el sistema no tiene la base de zonas horarias (tzdata). " +
                "Sin ella, la API no puede contar los días en la hora de Bolivia.";
        }

        CultureInfo culturaDeBolivia;
        try
        {
            culturaDeBolivia = CultureInfo.GetCultureInfo(cultura);
        }
        catch (CultureNotFoundException)
        {
            return $"No existe la cultura '{cultura}': .NET corre sin ICU (modo invariante). Quita " +
                "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT o instala ICU: sin ella, los montos no salen como en Bolivia.";
        }

        var monto = FormatoBolivia.Monto(1234.5m, culturaDeBolivia);
        return monto == MontoEsperado
            ? null
            : $"Los montos salen '{monto}' y deberían salir '{MontoEsperado}': revisa la versión de ICU del sistema.";
    }
}
