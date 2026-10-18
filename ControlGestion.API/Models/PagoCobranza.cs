using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.Models;

public enum MetodoPago { Efectivo = 0, Transferencia = 1, Cheque = 2, TarjetaDebito = 3, TarjetaCredito = 4 }

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

    public int? AnexoId { get; set; }
    public Anexo? Comprobante { get; set; }

    // La genera quien envía el pago. Si el mismo pago llega dos veces, la clave repetida evita duplicarlo.
    public Guid? ClaveIdempotencia { get; set; }
}
