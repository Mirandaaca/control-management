using System.ComponentModel.DataAnnotations;
using ControlGestion.API.Models;

namespace ControlGestion.API.DTOs;

public record RegistrarPagoDTO : IValidatableObject
{
    [Range(0, 99_999_999, MinimumIsExclusive = true, ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Monto { get; init; }

    [EnumDataType(typeof(MetodoPago), ErrorMessage = "El método de pago debe ser efectivo o QR.")]
    public MetodoPago MetodoPago { get; init; }

    [MaxLength(100, ErrorMessage = "La referencia no puede superar los 100 caracteres.")]
    public string? Referencia { get; init; }

    [MaxLength(500, ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
    public string? Observaciones { get; init; }

    // Opcional por ahora, para no romper clientes que todavía no la envían.
    public Guid? ClaveIdempotencia { get; init; }

    // Con QR, la referencia es lo que permite encontrar el pago en el banco (3.1).
    public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
    {
        if (MetodoPago == MetodoPago.QR && string.IsNullOrWhiteSpace(Referencia))
            yield return new ValidationResult("Escribe el número de referencia del pago con QR.", [nameof(Referencia)]);
    }
}

public record PagoRegistradoDTO(int Id, EstadoCobranza Estado, decimal MontoPagado, decimal SaldoPendiente);

/// <summary>Filtros de la lista. Desde y Hasta son días de Bolivia, ambos incluidos.</summary>
public record FiltrosCobranzaDTO(int? ClienteId, EstadoCobranza? Estado, string? PromotorId, DateOnly? Desde, DateOnly? Hasta);

public record MarcarIncobrableDTO
{
    [Required(ErrorMessage = "La observación es obligatoria.")]
    [MaxLength(500, ErrorMessage = "La observación no puede superar los 500 caracteres.")]
    public string Observacion { get; init; } = string.Empty;
}

public record CobranzaResumenDTO(
    int Id,
    string Numero,
    EstadoCobranza Estado,
    decimal MontoTotal,
    decimal MontoPagado,
    decimal SaldoPendiente,
    DateTime FechaEmision,
    DateTime FechaVencimiento,
    DateTime? FechaPagoCompleto,
    ClienteResumenDTO Cliente,
    UsuarioResumenDTO Promotor,
    HojaPedidoResumenDTO? HojaPedido,
    int CantidadPagos);

public record PagoDTO(
    int Id,
    decimal Monto,
    MetodoPago MetodoPago,
    DateTime FechaPago,
    string? Referencia,
    string? Observaciones,
    UsuarioResumenDTO RegistradoPor);

public record CobranzaDetalleDTO(
    int Id,
    string Numero,
    EstadoCobranza Estado,
    decimal MontoTotal,
    decimal MontoPagado,
    decimal SaldoPendiente,
    DateTime FechaEmision,
    DateTime FechaVencimiento,
    DateTime? FechaPagoCompleto,
    string? Observaciones,
    ClienteResumenDTO Cliente,
    UsuarioResumenDTO Promotor,
    HojaPedidoResumenDTO? HojaPedido,
    IReadOnlyList<PagoDTO> Pagos);
