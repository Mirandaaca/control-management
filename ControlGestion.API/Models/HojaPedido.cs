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

    // Token de concurrencia (mismo patrón que InventarioProducto): sin él, confirmar y anular la misma HP a la vez
    // podía dejarla anulada con el stock descontado y la deuda creada.
    public DateTime VersionConcurrencia { get; set; }

    // La deuda de una venta a crédito: una HP tiene a lo sumo una (7.3.5, paso 3.4; índice único en la base).
    public Cobranza? Cobranza { get; set; }

    // La genera la app al registrarla, como en PagoCobranza: si la misma venta llega dos veces (un reintento con mala
    // señal), se registra una sola vez y el stock se descuenta una vez.
    public Guid? ClaveIdempotencia { get; set; }
}
