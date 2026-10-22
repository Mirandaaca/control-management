namespace ControlGestion.API.Models;

/// <summary>
/// Un lugar que orienta en el mapa de una zona, como un mercado (5.5). Puede caer fuera del límite de su zona, que
/// quizás todavía no se dibujó. Se elimina con su zona.
/// </summary>
public class PuntoReferencia
{
    public int Id { get; set; }
    public int ZonaId { get; set; }
    public Zona? Zona { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Latitud { get; set; }
    public decimal Longitud { get; set; }
}
