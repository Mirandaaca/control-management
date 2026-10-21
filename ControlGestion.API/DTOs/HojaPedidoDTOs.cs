using System.ComponentModel.DataAnnotations;
using ControlGestion.API.Models;

namespace ControlGestion.API.DTOs;

public record DetalleHojaPedidoEntradaDTO
{
    [Range(1, int.MaxValue, ErrorMessage = "Elige el producto.")]
    public int ProductoId { get; init; }

    [Range(0.001, 9_999_999, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public decimal Cantidad { get; init; }

    /// <summary>Descuento por unidad, en Bs.</summary>
    [Range(0, 99_999_999, ErrorMessage = "El descuento no puede ser negativo.")]
    public decimal Descuento { get; init; }
}

/// <summary>La zona de la HP no se elige: es la del cliente.</summary>
public record RegistrarHojaPedidoDTO : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Elige el cliente.")]
    public int ClienteId { get; init; }

    // Sin ubicación no se guarda la HP: en la app la da el GPS sola (2.1).
    [Required(ErrorMessage = "Falta la ubicación de la venta.")]
    [Range(-90.0, 90.0, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
    public decimal? Latitud { get; init; }

    [Required(ErrorMessage = "Falta la ubicación de la venta.")]
    [Range(-180.0, 180.0, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
    public decimal? Longitud { get; init; }

    /// <summary>Precisión del GPS en metros, si el dispositivo la da.</summary>
    [Range(0, 100_000, ErrorMessage = "La precisión del GPS no puede ser negativa.")]
    public float? PrecisionGPS { get; init; }

    [EnumDataType(typeof(TipoVenta), ErrorMessage = "El tipo de venta debe ser contado o crédito.")]
    public TipoVenta TipoVenta { get; init; } = TipoVenta.Contado;

    /// <summary>Descuento sobre el total de la HP, en Bs.</summary>
    [Range(0, 99_999_999, ErrorMessage = "El descuento no puede ser negativo.")]
    public decimal Descuento { get; init; }

    [Required(ErrorMessage = "Agrega al menos un producto.")]
    [MinLength(1, ErrorMessage = "Agrega al menos un producto.")]
    public List<DetalleHojaPedidoEntradaDTO> Detalles { get; init; } = [];

    // 0,0 es lo que llega cuando nadie tomó la ubicación: un punto en el océano, no una venta real.
    public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
    {
        if (Latitud == 0 && Longitud == 0)
            yield return new ValidationResult("Falta la ubicación de la venta.", [nameof(Latitud)]);
    }
}

public record AnularHojaPedidoDTO
{
    [Required(ErrorMessage = "Escribe el motivo de la anulación.")]
    [MaxLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres.")]
    public string Motivo { get; init; } = string.Empty;
}

/// <summary>
/// Filtros de la lista de HP (query string). Buscar compara con el número de la HP y con el nombre y el código del
/// cliente. Desde y Hasta son días de Bolivia, ambos incluidos.
/// </summary>
public record FiltrosHojaPedidoDTO : IValidatableObject
{
    [MaxLength(100, ErrorMessage = "La búsqueda no puede superar los 100 caracteres.")]
    public string? Buscar { get; init; }

    public int? ClienteId { get; init; }
    public EstadoHP? Estado { get; init; }
    public TipoVenta? TipoVenta { get; init; }
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

public record HojaPedidoListaDTO(
    int Id,
    string Numero,
    EstadoHP EstadoHP,
    TipoVenta TipoVenta,
    decimal Total,
    DateTime FechaRegistro,
    ClienteResumenDTO Cliente,
    UsuarioResumenDTO Promotor,
    ZonaResumenDTO Zona);

public record DetalleHojaPedidoDTO(
    int Id,
    int ProductoId,
    string Sku,
    string Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Descuento,
    decimal Subtotal);

/// <summary>La cobranza que nació de una HP a crédito.</summary>
public record CobranzaReferenciaDTO(int Id, string Numero, EstadoCobranza Estado);

public record HojaPedidoDTO(
    int Id,
    string Numero,
    EstadoHP EstadoHP,
    TipoVenta TipoVenta,
    decimal SubTotal,
    decimal Descuento,
    decimal Total,
    DateTime FechaRegistro,
    decimal Latitud,
    decimal Longitud,
    float? PrecisionGPS,
    ClienteResumenDTO Cliente,
    UsuarioResumenDTO Promotor,
    ZonaResumenDTO Zona,
    string? ObservacionesAnulacion,
    DateTime? FechaAnulacion,
    UsuarioResumenDTO? AnuladoPor,
    CobranzaReferenciaDTO? Cobranza,
    IReadOnlyList<DetalleHojaPedidoDTO> Detalles);
