using System.ComponentModel.DataAnnotations;
using ControlGestion.API.Models;

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

    /// <summary>La misma clave devuelve el pedido ya registrado, sin crear otro.</summary>
    public Guid? ClaveIdempotencia { get; init; }
}

public record AvanzarPedidoDTO
{
    [MaxLength(500, ErrorMessage = "La observación no puede superar los 500 caracteres.")]
    public string? Observacion { get; init; }
}

/// <summary>Un pedido elegido para avanzar, con el estado en que lo vio quien lo eligió.</summary>
public record PedidoQueAvanzaDTO
{
    [Range(1, int.MaxValue, ErrorMessage = "Elige un pedido.")]
    public int Id { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Falta el estado del pedido.")]
    public int EstadoId { get; init; }
}

/// <summary>Avanzar varios pedidos a la vez (1.2): cada uno a su estado siguiente, con la misma observación.</summary>
public record AvanzarPedidosDTO
{
    [Required(ErrorMessage = "Elige al menos un pedido.")]
    [MinLength(1, ErrorMessage = "Elige al menos un pedido.")]
    [MaxLength(100, ErrorMessage = "Se avanzan hasta 100 pedidos a la vez.")]
    public List<PedidoQueAvanzaDTO> Pedidos { get; init; } = [];

    [MaxLength(500, ErrorMessage = "La observación no puede superar los 500 caracteres.")]
    public string? Observacion { get; init; }
}

public record ReprogramarPedidoDTO
{
    /// <summary>Sin zona se entiende como hora de Bolivia, que es lo que manda el panel.</summary>
    [Required(ErrorMessage = "Elige la nueva fecha y hora de entrega.")]
    public DateTime? FechaEntregaEstimada { get; init; }

    [Required(ErrorMessage = "Escribe el motivo de la reprogramación.")]
    [MaxLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres.")]
    public string Motivo { get; init; } = string.Empty;
}

/// <summary>Distribución (1.3): uno o varios pedidos a un promotor y a un día de reparto.</summary>
public record AsignarPedidosDTO
{
    [Required(ErrorMessage = "Elige al menos un pedido.")]
    [MinLength(1, ErrorMessage = "Elige al menos un pedido.")]
    [MaxLength(100, ErrorMessage = "Se asignan hasta 100 pedidos a la vez.")]
    public List<int> PedidoIds { get; init; } = [];

    [Required(ErrorMessage = "Elige el promotor.")]
    public string PromotorId { get; init; } = string.Empty;

    /// <summary>Día de Bolivia del reparto: cada pedido pasa a ese día con su misma hora.</summary>
    [Required(ErrorMessage = "Elige el día de entrega.")]
    public DateOnly? Dia { get; init; }

    /// <summary>"Ya salen a ruta": los pendientes pasan además a En ruta. Solo con el día de hoy.</summary>
    public bool SalenARuta { get; init; }
}

public record AnularPedidoDTO
{
    [Required(ErrorMessage = "Escribe el motivo de la anulación.")]
    [MaxLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres.")]
    public string Motivo { get; init; } = string.Empty;
}

/// <summary>La entrega (1.2): se entrega todo el pedido, y la venta se cierra en ese momento.</summary>
public record EntregarPedidoDTO : IValidatableObject
{
    [Required(ErrorMessage = "Escribe quién recibió el pedido.")]
    [MaxLength(150, ErrorMessage = "El nombre de quien recibió no puede superar los 150 caracteres.")]
    public string RecibidoPor { get; init; } = string.Empty;

    [Required(ErrorMessage = "Elige si la venta es al contado o a crédito.")]
    [EnumDataType(typeof(TipoVenta), ErrorMessage = "El tipo de venta debe ser contado o crédito.")]
    public TipoVenta? TipoVenta { get; init; }

    // Sin ubicación no se registra la entrega: en la app la da el GPS sola, como en la HP.
    [Required(ErrorMessage = "Falta la ubicación de la entrega.")]
    [Range(-90.0, 90.0, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
    public decimal? Latitud { get; init; }

    [Required(ErrorMessage = "Falta la ubicación de la entrega.")]
    [Range(-180.0, 180.0, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
    public decimal? Longitud { get; init; }

    /// <summary>Precisión del GPS en metros, si el dispositivo la da.</summary>
    [Range(0, 100_000, ErrorMessage = "La precisión del GPS no puede ser negativa.")]
    public float? PrecisionGPS { get; init; }

    [MaxLength(500, ErrorMessage = "La observación no puede superar los 500 caracteres.")]
    public string? Observacion { get; init; }

    // 0,0 es lo que llega cuando nadie tomó la ubicación: un punto en el océano, no una entrega real.
    public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
    {
        if (Latitud == 0 && Longitud == 0)
            yield return new ValidationResult("Falta la ubicación de la entrega.", [nameof(Latitud)]);
    }
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

    /// <summary>El promotor que tiene asignado el pedido para entregarlo.</summary>
    public string? RepartidorId { get; init; }

    public bool SinAsignar { get; init; }
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

public record TotalPorEstadoDTO(int EstadoId, string Estado, int Cantidad, decimal Monto);

/// <summary>
/// El reporte de pedidos (1.4): los cuatro estados con su cantidad y su monto, el total sin los anulados y los pedidos,
/// paginados.
/// </summary>
public record ReportePedidosDTO(IReadOnlyList<TotalPorEstadoDTO> PorEstado, int Cantidad, decimal Monto, Pagina<PedidoListaDTO> Pedidos);

/// <summary>La HP que nació de la entrega, con su estado: si la secretaria la anula, el pedido sigue entregado.</summary>
public record HojaDeLaEntregaDTO(int Id, string Numero, EstadoHP EstadoHP, TipoVenta TipoVenta, CobranzaReferenciaDTO? Cobranza);

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
    string? RecibidoPor,
    decimal? LatitudEntrega,
    decimal? LongitudEntrega,
    ClienteResumenDTO Cliente,
    UsuarioResumenDTO? Promotor,
    UsuarioResumenDTO RegistradoPor,
    UsuarioResumenDTO? Repartidor,
    ZonaResumenDTO Zona,
    HojaDeLaEntregaDTO? HojaPedido,
    IReadOnlyList<DetallePedidoDTO> Detalles,
    IReadOnlyList<HistorialPedidoDTO> Historial);
