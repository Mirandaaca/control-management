namespace ControlGestion.API.Models;

public class DetallePedido
{
    public int Id { get; set; }

    public int PedidoId { get; set; }
    public Pedido? Pedido { get; set; }

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Descuento { get; set; } = 0;
    public decimal Subtotal { get; set; }
    public decimal CantidadEntregada { get; set; } = 0;
}
