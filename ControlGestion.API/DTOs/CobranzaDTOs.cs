using ControlGestion.API.Models;

namespace ControlGestion.API.DTOs;

public record RegistrarPagoDTO(
    decimal Monto,
    MetodoPago MetodoPago,
    string? Referencia,
    string? Observaciones,
    // Opcional por ahora, para no romper clientes que todavía no la envían.
    Guid? ClaveIdempotencia = null
);

public record PagoRegistradoDTO(int Id, EstadoCobranza Estado, decimal MontoPagado, decimal SaldoPendiente);
