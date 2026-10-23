using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.Models;

/// <summary>Los estados de la 7.3.20: se planifica, el promotor la inicia y la termina.</summary>
public enum EstadoRuta { Planificada = 0, EnCurso = 1, Completada = 2 }

public class Ruta
{
    public int Id { get; set; }

    [MaxLength(20)]
    public string Numero { get; set; } = string.Empty;

    /// <summary>Lo pone el sistema al planificarla ("Zona Norte · 30/10/2026"): el formulario de la 7.3.20 no lo pide.</summary>
    [MaxLength(200)]
    public string Nombre { get; set; } = string.Empty;

    public string PromotorId { get; set; } = string.Empty;
    public ApplicationUser? Promotor { get; set; }

    public int ZonaId { get; set; }
    public Zona? Zona { get; set; }

    /// <summary>El día de Bolivia de la ruta. Un promotor tiene una sola ruta por día (índice único, 7.3.20 ext. 2.3b).</summary>
    public DateOnly FechaPlanificada { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }

    public EstadoRuta Estado { get; set; } = EstadoRuta.Planificada;

    [MaxLength(500)]
    public string? Observaciones { get; set; }

    public ICollection<RutaDetalle> Detalles { get; set; } = new List<RutaDetalle>();
}
