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

/// <summary>
/// Filtros de la lista (query string). Buscar compara con el número de la cobranza y con el nombre y el código del
/// cliente. Desde y Hasta son días de Bolivia, ambos incluidos, sobre la fecha de emisión.
/// </summary>
public record FiltrosCobranzaDTO : IValidatableObject
{
    [MaxLength(100, ErrorMessage = "La búsqueda no puede superar los 100 caracteres.")]
    public string? Buscar { get; init; }

    public int? ClienteId { get; init; }
    public EstadoCobranza? Estado { get; init; }
    public string? PromotorId { get; init; }
    public DateOnly? Desde { get; init; }
    public DateOnly? Hasta { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "La página empieza en 1.")]
    public int Pagina { get; init; } = 1;

    [Range(1, 100, ErrorMessage = "El tamaño de la página va de 1 a 100.")]
    public int Tamano { get; init; } = 25;

    public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
    {
        if (Desde > Hasta)
            yield return new ValidationResult("La fecha \"desde\" no puede ser posterior a \"hasta\".", [nameof(Desde)]);
    }
}

/// <summary>
/// Lo que se debe en las cobranzas abiertas que cumplen los filtros: cuánto hay por cobrar, cuántas están al día
/// (pendientes o parciales) y cuántas vencidas.
/// </summary>
public record ResumenCobranzasDTO(decimal PorCobrar, int Pendientes, int Vencidas);

public record AnularPagoDTO
{
    [Required(ErrorMessage = "Escribe el motivo de la anulación.")]
    [MaxLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres.")]
    public string Motivo { get; init; } = string.Empty;
}

/// <summary>El comprobante de un pago (multipart): una foto JPEG, PNG o WebP.</summary>
public record AdjuntarComprobanteDTO
{
    [Required(ErrorMessage = "Elige la foto del comprobante.")]
    public IFormFile? Archivo { get; init; }
}

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
    /// <summary>Los pagos válidos: sin contar los anulados.</summary>
    int CantidadPagos);

public record PagoDTO(
    int Id,
    decimal Monto,
    MetodoPago MetodoPago,
    DateTime FechaPago,
    string? Referencia,
    string? Observaciones,
    UsuarioResumenDTO RegistradoPor,
    bool Anulado,
    string? MotivoAnulacion,
    /// <summary>La foto del comprobante, si tiene: se abre con GET /evidencias/{id}/archivo.</summary>
    int? ComprobanteId);

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
