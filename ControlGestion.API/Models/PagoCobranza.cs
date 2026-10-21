using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.Models;

/// <summary>Efectivo o QR (3.1). El QR lleva su número de referencia.</summary>
public enum MetodoPago { Efectivo = 0, QR = 1 }

public class PagoCobranza
{
    public int Id { get; set; }

    public int CobranzaId { get; set; }
    public Cobranza? Cobranza { get; set; }

    public string RegistradoPorId { get; set; } = string.Empty;
    public ApplicationUser? RegistradoPor { get; set; }

    public decimal Monto { get; set; }
    public MetodoPago MetodoPago { get; set; } = MetodoPago.Efectivo;

    public DateTime FechaPago { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? Referencia { get; set; }

    [MaxLength(500)]
    public string? Observaciones { get; set; }

    /// <summary>El comprobante del pago: una foto opcional (3.1). Uno nuevo reemplaza al anterior.</summary>
    public int? EvidenciaId { get; set; }
    public Evidencia? Comprobante { get; set; }

    // La genera quien envía el pago. Si el mismo pago llega dos veces, la clave repetida evita duplicarlo.
    public Guid? ClaveIdempotencia { get; set; }

    // Un pago anulado no se borra: queda en el historial con su motivo y deja de contar (3.1, 7.5).
    public bool Anulado { get; set; }

    [MaxLength(500)]
    public string? MotivoAnulacion { get; set; }
}
