namespace ControlGestion.API.Models;

public class Pedido
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;

    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    // El promotor del cliente al registrar (7.5). Queda vacío si la zona del cliente todavía no tiene promotor.
    public string? PromotorId { get; set; }
    public ApplicationUser? Promotor { get; set; }

    public string RegistradoPorId { get; set; } = string.Empty;
    public ApplicationUser? RegistradoPor { get; set; }

    public string? RepartidorId { get; set; }
    public ApplicationUser? Repartidor { get; set; }

    public int ZonaId { get; set; }
    public Zona? Zona { get; set; }

    // La venta que nació de la entrega (1.2).
    public int? HojaPedidoId { get; set; }
    public HojaPedido? HojaPedido { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public DateTime? FechaEntregaEstimada { get; set; }
    public DateTime? FechaEntregaReal { get; set; }
    public string DireccionEntrega { get; set; } = string.Empty;
    public string? RecibidoPor { get; set; }

    public decimal? LatitudEntrega { get; set; }
    public decimal? LongitudEntrega { get; set; }

    public int EstadoPedidoId { get; set; }
    public EstadoPedido? EstadoPedido { get; set; }

    public decimal SubTotal { get; set; }
    public decimal Descuento { get; set; } = 0;
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }

    // Token de concurrencia (mismo patrón que HojaPedido): sin él, dos cambios a la vez sobre el mismo pedido (anularlo
    // mientras se entrega, por ejemplo) se pisarían.
    public DateTime VersionConcurrencia { get; set; }

    public ICollection<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();
    public ICollection<HistorialEstadoPedido> Historial { get; set; } = new List<HistorialEstadoPedido>();
}
