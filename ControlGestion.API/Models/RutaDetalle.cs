using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.Models;

public enum EstadoVisita { Pendiente = 0, Visitado = 1, Ausente = 2, Reprogramado = 3 }

public class RutaDetalle
{
    public int Id { get; set; }

    public int RutaId { get; set; }
    public Ruta? Ruta { get; set; }

    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public int Orden { get; set; }

    public EstadoVisita EstadoVisita { get; set; } = EstadoVisita.Pendiente;

    public DateTime? HoraVisita { get; set; }

    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }

    [MaxLength(500)]
    public string? Observaciones { get; set; }
}
