using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

public class DetallePedidoEntradaDTO
{
    [Required] public int ProductoId { get; set; }
    [Required][Range(0.001, double.MaxValue)] public decimal Cantidad { get; set; }
    public decimal Descuento { get; set; } = 0;
}

public class PedidoCreateDTO
{
    [Required] public int ClienteId { get; set; }
    [Required] public int ZonaId { get; set; }
    public int? HojaPedidoId { get; set; }
    public DateTime? FechaEntregaEstimada { get; set; }
    public decimal Descuento { get; set; } = 0;
    public string? Observaciones { get; set; }
    [Required][MinLength(1)] public List<DetallePedidoEntradaDTO> Detalles { get; set; } = new();
}

public class CambiarEstadoDTO
{
    [Required] public int NuevoEstadoId { get; set; }
    public string? Observacion { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public decimal? CantidadEntregada { get; set; }
}

/// <summary>
/// Filtros de la lista de pedidos (query string). Buscar compara con el número del pedido y con el nombre y el código
/// del cliente. Desde y Hasta son días de Bolivia de la fecha de entrega, ambos incluidos.
/// </summary>
public record FiltrosPedidoDTO : IValidatableObject
{
    [MaxLength(100, ErrorMessage = "La búsqueda no puede superar los 100 caracteres.")]
    public string? Buscar { get; init; }

    public int? ClienteId { get; init; }
    public int? EstadoId { get; init; }
    public int? ZonaId { get; init; }
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

public record EstadoPedidoDTO(int Id, string Nombre);

public record PedidoListaDTO(
    int Id,
    string Numero,
    EstadoPedidoDTO Estado,
    decimal Total,
    DateTime FechaRegistro,
    DateTime? FechaEntregaEstimada,
    ClienteResumenDTO Cliente,
    UsuarioResumenDTO Promotor,
    UsuarioResumenDTO? Repartidor,
    ZonaResumenDTO Zona);

public record DetallePedidoDTO(
    int Id,
    int ProductoId,
    string Sku,
    string Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Descuento,
    decimal Subtotal,
    decimal CantidadEntregada);

public record HistorialPedidoDTO(
    int Id,
    EstadoPedidoDTO Estado,
    DateTime Fecha,
    UsuarioResumenDTO Usuario,
    string? Observacion,
    decimal? Latitud,
    decimal? Longitud);

public record PedidoDTO(
    int Id,
    string Numero,
    EstadoPedidoDTO Estado,
    decimal SubTotal,
    decimal Descuento,
    decimal Total,
    string? Observaciones,
    DateTime FechaRegistro,
    DateTime? FechaEntregaEstimada,
    DateTime? FechaEntregaReal,
    decimal? LatitudEntrega,
    decimal? LongitudEntrega,
    ClienteResumenDTO Cliente,
    UsuarioResumenDTO Promotor,
    UsuarioResumenDTO? Repartidor,
    ZonaResumenDTO Zona,
    HojaPedidoResumenDTO? HojaPedido,
    IReadOnlyList<DetallePedidoDTO> Detalles,
    IReadOnlyList<HistorialPedidoDTO> Historial);
