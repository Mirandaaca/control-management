using System.Globalization;
using ControlGestion.API.Models;

namespace ControlGestion.API.Tests;

/// <summary>La paleta de los estados manuales (7.4) cumple las reglas de Cristopher: no se lee la base.</summary>
public class PaletaDeEstadosTests
{
    private static (double R, double G, double B) Rgb(string hex) =>
        (int.Parse(hex[1..3], NumberStyles.HexNumber) / 255d,
         int.Parse(hex[3..5], NumberStyles.HexNumber) / 255d,
         int.Parse(hex[5..7], NumberStyles.HexNumber) / 255d);

    // Luminancia relativa y contraste de WCAG 2.
    private static double Luminancia(string hex)
    {
        static double Lineal(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        var (r, g, b) = Rgb(hex);
        return 0.2126 * Lineal(r) + 0.7152 * Lineal(g) + 0.0722 * Lineal(b);
    }

    private static double Contraste(string a, string b)
    {
        var (la, lb) = (Luminancia(a), Luminancia(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static (double Tono, double Saturacion) TonoYSaturacion(string hex)
    {
        var (r, g, b) = Rgb(hex);
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        var luz = (max + min) / 2;
        if (delta == 0) return (0, 0);
        var saturacion = delta / (1 - Math.Abs(2 * luz - 1));
        var tono = max == r ? 60 * ((g - b) / delta % 6) : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
        return (tono < 0 ? tono + 360 : tono, saturacion);
    }

    [Fact]
    public void Cada_color_tiene_4_5_a_1_o_mas_con_su_texto_y_con_el_blanco()
    {
        foreach (var color in PaletaDeEstados.Manuales)
        {
            Assert.True(Contraste(color.Color, color.Texto) >= 4.5, $"{color.Nombre} con su texto: {Contraste(color.Color, color.Texto):0.0}:1");
            Assert.True(Contraste(color.Color, "#FFFFFF") >= 4.5, $"{color.Nombre} con el blanco: {Contraste(color.Color, "#FFFFFF"):0.0}:1");
        }
    }

    [Fact]
    public void No_tiene_el_rojo_ni_el_verde_ni_tonos_que_se_confundan_con_ellos()
    {
        Assert.Null(PaletaDeEstados.Normalizar(PaletaDeEstados.Activo));
        Assert.Null(PaletaDeEstados.Normalizar(PaletaDeEstados.Disponible));
        // Fuera de los grises, solo azules y violetas: del 190 al 300 del círculo. Quedan fuera el rojo, el naranja y el
        // amarillo (0 a 60), la lima, el verde y el verde azulado (60 a 190) y el rosa (300 a 360).
        foreach (var color in PaletaDeEstados.Manuales)
        {
            var (tono, saturacion) = TonoYSaturacion(color.Color);
            Assert.True(saturacion < 0.3 || tono is >= 190 and <= 300, $"{color.Nombre}: tono {tono:0}");
        }
    }

    [Fact]
    public void Normalizar_acepta_cualquier_mayuscula_y_rechaza_lo_que_no_es_de_la_paleta()
    {
        Assert.Equal("#1D4ED8", PaletaDeEstados.Normalizar(" #1d4ed8 "));
        Assert.Null(PaletaDeEstados.Normalizar("#123456"));
        Assert.Null(PaletaDeEstados.Normalizar(null));
    }
}
