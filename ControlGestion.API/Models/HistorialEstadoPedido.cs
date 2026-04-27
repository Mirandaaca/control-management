namespace ControlGestion.API.Models;

public class HistorialEstadoPedido
{
    public int Id { get; set; }

    public int PedidoId { get; set; }
    public Pedido? Pedido { get; set; }

    public int EstadoPedidoId { get; set; }
    public EstadoPedido? EstadoPedido { get; set; }

    public string UsuarioId { get; set; } = string.Empty;
    public ApplicationUser? Usuario { get; set; }

    public DateTime FechaCambio { get; set; } = DateTime.UtcNow;
    public string? Observacion { get; set; }
    public decimal? LatitudCambio { get; set; }
    public decimal? LongitudCambio { get; set; }
}
