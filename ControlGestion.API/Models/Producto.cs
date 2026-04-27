namespace ControlGestion.API.Models;

public class Producto
{
    public int Id { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public bool Activo { get; set; } = true;
    public string? Categoria { get; set; }
}
