using System.ComponentModel.DataAnnotations;
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
