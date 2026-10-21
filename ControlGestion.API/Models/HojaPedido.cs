namespace ControlGestion.API.Models;

public enum TipoVenta { Contado, Credito }
public enum EstadoHP { Borrador, Confirmada, Anulada }

public class HojaPedido
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;

    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public string PromotorId { get; set; } = string.Empty;
    public ApplicationUser? Promotor { get; set; }

    public int ZonaId { get; set; }
    public Zona? Zona { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public decimal Latitud { get; set; }
    public decimal Longitud { get; set; }
    public float? PrecisionGPS { get; set; }

    public TipoVenta TipoVenta { get; set; } = TipoVenta.Contado;

    public decimal SubTotal { get; set; }
    public decimal Descuento { get; set; } = 0;
    public decimal Total { get; set; }

    public EstadoHP EstadoHP { get; set; } = EstadoHP.Borrador;
    public string? ObservacionesAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }
    public string? AnuladoPorId { get; set; }
    public ApplicationUser? AnuladoPor { get; set; }

    public ICollection<DetalleHP> Detalles { get; set; } = new List<DetalleHP>();

    // Una HP a crédito tiene una sola cobranza; es una colección porque la relación de Cobranza ya era de uno a muchos.
    public ICollection<Cobranza> Cobranzas { get; set; } = new List<Cobranza>();
}
