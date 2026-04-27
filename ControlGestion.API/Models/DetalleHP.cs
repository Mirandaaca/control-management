namespace ControlGestion.API.Models;

public class DetalleHP
{
    public int Id { get; set; }

    public int HojaPedidoId { get; set; }
    public HojaPedido? HojaPedido { get; set; }

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Descuento { get; set; } = 0;
    public decimal Subtotal { get; set; }
}
