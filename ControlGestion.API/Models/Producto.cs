namespace ControlGestion.API.Models;

public class Producto
{
    public int Id { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Precio de venta: el que se cobra en la HP y en el pedido.</summary>
    public decimal Precio { get; set; }
    public decimal PrecioCompra { get; set; }

    public bool Activo { get; set; } = true;
    public string? Categoria { get; set; }

    public InventarioProducto? Inventario { get; set; }
}
