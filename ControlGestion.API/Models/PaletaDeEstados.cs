namespace ControlGestion.API.Models;

/// <summary>Un color de la paleta de los estados del cliente, con el color del texto que va encima.</summary>
public record ColorDeEstado(string Nombre, string Color, string Texto);

/// <summary>
/// Los colores de los estados del cliente (7.4). Rojo y verde son de Activo y Disponible para re-visita y tienen
/// significado fijo (docs/diseno.md, sección 2); los estados asignados a mano eligen de <see cref="Manuales"/>, que no
/// tiene tonos que se confundan con ellos (naranja, rosa, lima ni verde azulado). Cada color tiene 4,5:1 o más con su
/// texto y con el blanco, así también sirve para el marcador del mapa. La copia del panel está en
/// control-gestion-ui/src/components/paletaDeEstados.js: si cambia aquí, cambia allá.
/// </summary>
public static class PaletaDeEstados
{
    public const string Activo = "#DC2626";
    public const string Disponible = "#15803D";
    public const string Pizarra = "#475569";

    public static readonly IReadOnlyList<ColorDeEstado> Manuales =
    [
        new("Pizarra", Pizarra, "#FFFFFF"),
        new("Grafito", "#1E293B", "#FFFFFF"),
        new("Azul", "#1D4ED8", "#FFFFFF"),
        new("Celeste", "#0369A1", "#FFFFFF"),
        new("Índigo", "#4338CA", "#FFFFFF"),
        new("Violeta", "#6D28D9", "#FFFFFF")
    ];

    /// <summary>El color tal como está en la paleta (en mayúsculas), o null si no es de ella.</summary>
    public static string? Normalizar(string? color) =>
        Manuales.FirstOrDefault(c => string.Equals(c.Color, color?.Trim(), StringComparison.OrdinalIgnoreCase))?.Color;
}
