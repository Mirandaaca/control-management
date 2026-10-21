namespace ControlGestion.API.Models;

/// <summary>Pendiente mientras se atiende; Resuelto con su solución (7.5). Un evento resuelto ya no cambia.</summary>
public enum EstadoEvento { Pendiente = 0, Resuelto = 1 }

public class Evento
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;

    public int TipoEventoId { get; set; }
    public TipoEvento? TipoEvento { get; set; }

    // Siempre se guarda el cliente: si el evento es de un pedido, es el cliente del pedido.
    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public int? PedidoId { get; set; }
    public Pedido? Pedido { get; set; }

    /// <summary>Quien registró el evento.</summary>
    public string PromotorId { get; set; } = string.Empty;
    public ApplicationUser? Promotor { get; set; }

    public DateTime FechaEvento { get; set; }
    public decimal Latitud { get; set; }
    public decimal Longitud { get; set; }
    public string Descripcion { get; set; } = string.Empty;

    public EstadoEvento EstadoEvento { get; set; } = EstadoEvento.Pendiente;
    public string? Solucion { get; set; }
    public string? ResueltoPorId { get; set; }
    public ApplicationUser? ResueltoPor { get; set; }
    public DateTime? FechaResolucion { get; set; }

    // Token de concurrencia (mismo patrón que Pedido): sin él, modificar un evento mientras otro lo resuelve dejaría
    // un evento resuelto con datos cambiados después.
    public DateTime VersionConcurrencia { get; set; }

    public ICollection<Evidencia> Evidencias { get; set; } = new List<Evidencia>();
}
