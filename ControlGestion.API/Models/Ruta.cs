using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.Models;

public enum EstadoRuta { Planificada = 0, EnCurso = 1, Completada = 2, Cancelada = 3 }

public class Ruta
{
    public int Id { get; set; }

    [MaxLength(20)]
    public string Numero { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Nombre { get; set; } = string.Empty;

    public string PromotorId { get; set; } = string.Empty;
    public ApplicationUser? Promotor { get; set; }

    public int ZonaId { get; set; }
    public Zona? Zona { get; set; }

    public DateTime FechaPlanificada { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }

    public EstadoRuta Estado { get; set; } = EstadoRuta.Planificada;

    [MaxLength(500)]
    public string? Observaciones { get; set; }

    public ICollection<RutaDetalle> Detalles { get; set; } = new List<RutaDetalle>();
}
