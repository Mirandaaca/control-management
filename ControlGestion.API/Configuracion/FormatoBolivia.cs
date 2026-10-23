using System.Globalization;

namespace ControlGestion.API.Configuracion;

/// <summary>Los números de los mensajes, como se leen en Bolivia: "Bs 1.234,50" y "12,5".</summary>
public static class FormatoBolivia
{
    public const string IdDeCultura = "es-BO";

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo(IdDeCultura);

    public static string Monto(decimal monto) => Monto(monto, Cultura);

    internal static string Monto(decimal monto, CultureInfo cultura) => $"Bs {monto.ToString("N2", cultura)}";

    /// <summary>Una cantidad sin ceros de más: "85" o "12,5".</summary>
    public static string Cantidad(decimal cantidad) => cantidad.ToString("0.###", Cultura);
}
