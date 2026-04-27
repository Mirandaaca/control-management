namespace ControlGestion.API.Models;

public class Pedido
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;

    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public string PromotorId { get; set; } = string.Empty;
    public ApplicationUser? Promotor { get; set; }

    public string? RepartidorId { get; set; }
    public ApplicationUser? Repartidor { get; set; }

    public int ZonaId { get; set; }
    public Zona? Zona { get; set; }

    public int? HojaPedidoId { get; set; }
    public HojaPedido? HojaPedido { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public DateTime? FechaEntregaEstimada { get; set; }
    public DateTime? FechaEntregaReal { get; set; }

    public decimal? LatitudEntrega { get; set; }
    public decimal? LongitudEntrega { get; set; }

    public int EstadoPedidoId { get; set; }
    public EstadoPedido? EstadoPedido { get; set; }

    public decimal SubTotal { get; set; }
    public decimal Descuento { get; set; } = 0;
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }

    public ICollection<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();
    public ICollection<HistorialEstadoPedido> Historial { get; set; } = new List<HistorialEstadoPedido>();
}
