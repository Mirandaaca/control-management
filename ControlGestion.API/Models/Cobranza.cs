using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.Models;

public enum EstadoCobranza { Pendiente = 0, Parcial = 1, Pagada = 2, Vencida = 3, Incobrable = 4 }

public class Cobranza
{
    public int Id { get; set; }

    [MaxLength(20)]
    public string Numero { get; set; } = string.Empty;

    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public int? HojaPedidoId { get; set; }
    public HojaPedido? HojaPedido { get; set; }

    public string PromotorId { get; set; } = string.Empty;
    public ApplicationUser? Promotor { get; set; }

    public decimal MontoTotal { get; set; }
    public decimal MontoPagado { get; set; }
    public decimal SaldoPendiente => MontoTotal - MontoPagado;

    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;
    public DateTime FechaVencimiento { get; set; }
    public DateTime? FechaPagoCompleto { get; set; }

    public EstadoCobranza Estado { get; set; } = EstadoCobranza.Pendiente;

    [MaxLength(500)]
    public string? Observaciones { get; set; }

    public ICollection<PagoCobranza> Pagos { get; set; } = new List<PagoCobranza>();

    // Token de concurrencia (mismo patrón que InventarioProducto): MySQL lo cambia en cada UPDATE.
    public DateTime VersionConcurrencia { get; set; }
}
