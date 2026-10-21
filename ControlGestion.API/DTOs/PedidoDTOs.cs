using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

public record LineaDePedidoDTO
{
    [Range(1, int.MaxValue, ErrorMessage = "Elige el producto.")]
    public int ProductoId { get; init; }

    [Range(0.001, 9_999_999, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public decimal Cantidad { get; init; }

    /// <summary>Descuento por unidad, en Bs.</summary>
    [Range(0, 99_999_999, ErrorMessage = "El descuento no puede ser negativo.")]
    public decimal Descuento { get; init; }
}

/// <summary>Lo que se puede cambiar de un pedido (1.1): todo menos el cliente.</summary>
public record ModificarPedidoDTO
{
    /// <summary>Sin zona se entiende como hora de Bolivia, que es lo que manda el panel.</summary>
    [Required(ErrorMessage = "Elige la fecha y la hora de entrega.")]
    public DateTime? FechaEntregaEstimada { get; init; }

    [Required(ErrorMessage = "Escribe la dirección de entrega.")]
    [MaxLength(300, ErrorMessage = "La dirección no puede superar los 300 caracteres.")]
    public string DireccionEntrega { get; init; } = string.Empty;

    /// <summary>Descuento sobre el total del pedido, en Bs.</summary>
    [Range(0, 99_999_999, ErrorMessage = "El descuento no puede ser negativo.")]
    public decimal Descuento { get; init; }

    [MaxLength(500, ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
    public string? Observaciones { get; init; }

    [Required(ErrorMessage = "Agrega al menos un producto.")]
    [MinLength(1, ErrorMessage = "Agrega al menos un producto.")]
    public List<LineaDePedidoDTO> Detalles { get; init; } = [];
}

/// <summary>La zona y el promotor del pedido no se eligen: son los del cliente.</summary>
public record RegistrarPedidoDTO : ModificarPedidoDTO
{
    [Range(1, int.MaxValue, ErrorMessage = "Elige el cliente.")]
    public int ClienteId { get; init; }
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
    string DireccionEntrega,
    ClienteResumenDTO Cliente,
    UsuarioResumenDTO? Promotor,
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
    string DireccionEntrega,
    decimal? LatitudEntrega,
    decimal? LongitudEntrega,
    ClienteResumenDTO Cliente,
    UsuarioResumenDTO? Promotor,
    UsuarioResumenDTO RegistradoPor,
    UsuarioResumenDTO? Repartidor,
    ZonaResumenDTO Zona,
    HojaPedidoResumenDTO? HojaPedido,
    IReadOnlyList<DetallePedidoDTO> Detalles,
    IReadOnlyList<HistorialPedidoDTO> Historial);
