namespace ControlGestion.API.Models;

public enum TipoMovimiento
{
    Entrada,
    Salida,
    Ajuste,
    DevolucionVenta
}

public class MovimientoInventario
{
    // ReferenciaTabla dice de dónde salió el movimiento; con HojaPedido, ReferenciaId es el id de la HP.
    public const string ReferenciaHojaPedido = "HojaPedido";
    public const string ReferenciaIngreso = "Ingreso";

    public int Id { get; set; }
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public TipoMovimiento TipoMovimiento { get; set; }
    public decimal Cantidad { get; set; }
    public decimal StockAnterior { get; set; }
    public decimal StockResultante { get; set; }

    public string ReferenciaTabla { get; set; } = string.Empty;
    public int ReferenciaId { get; set; }

    public string UsuarioId { get; set; } = string.Empty;
    public ApplicationUser? Usuario { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? Observacion { get; set; }
}
