namespace ControlGestion.API.Models;

public class InventarioProducto
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public decimal StockActual { get; set; } = 0;
    public decimal StockMinimo { get; set; } = 0;
    public decimal StockMaximo { get; set; } = 9999;
    public DateTime UltimaActualizacion { get; set; } = DateTime.UtcNow;

    public DateTime VersionConcurrencia { get; set; }
}
