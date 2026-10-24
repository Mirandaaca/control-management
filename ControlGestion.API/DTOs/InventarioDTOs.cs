using System.ComponentModel.DataAnnotations;
using ControlGestion.API.Models;

namespace ControlGestion.API.DTOs;

/// <summary>Un ingreso de mercadería: solo suma al stock (2.5). No hay ajustes con signo.</summary>
public record RegistrarIngresoDTO
{
    [Range(0, 9_999_999, MinimumIsExclusive = true, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public decimal Cantidad { get; init; }

    [Required(ErrorMessage = "Escribe de dónde viene la mercadería, por ejemplo el proveedor o la factura.")]
    [MaxLength(300, ErrorMessage = "La observación no puede superar los 300 caracteres.")]
    public string Observacion { get; init; } = string.Empty;
}

/// <summary>Filtros de la lista de inventario. Buscar compara con el código, la descripción y la categoría.</summary>
public record FiltrosInventarioDTO
{
    [MaxLength(100, ErrorMessage = "La búsqueda no puede superar los 100 caracteres.")]
    public string? Buscar { get; init; }

    /// <summary>Solo los productos con el stock igual o menor que su mínimo.</summary>
    public bool SoloBajoMinimo { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "La página empieza en 1.")]
    public int Pagina { get; init; } = 1;

    [Range(1, 100, ErrorMessage = "El tamaño de la página va de 1 a 100.")]
    public int Tamano { get; init; } = 25;
}

public record FiltrosKardexDTO
{
    [Range(1, int.MaxValue, ErrorMessage = "La página empieza en 1.")]
    public int Pagina { get; init; } = 1;

    [Range(1, 100, ErrorMessage = "El tamaño de la página va de 1 a 100.")]
    public int Tamano { get; init; } = 25;
}

public record InventarioDTO(
    int ProductoId,
    string Sku,
    string Descripcion,
    string? Categoria,
    /// <summary>El precio de venta: la secretaria lo necesita al buscar productos (7.3.7, paso 2.2).</summary>
    decimal PrecioVenta,
    decimal StockActual,
    decimal StockMinimo,
    bool BajoMinimo,
    DateTime? UltimaActualizacion);

/// <summary>
/// Un movimiento del kardex. Cantidad va siempre en positivo; Variacion trae el signo (lo que el movimiento sumó o
/// restó al stock). HojaPedido es la HP que lo originó, si la hay.
/// </summary>
public record MovimientoDTO(
    int Id,
    DateTime Fecha,
    TipoMovimiento TipoMovimiento,
    decimal Cantidad,
    decimal Variacion,
    decimal StockAnterior,
    decimal StockResultante,
    HojaPedidoResumenDTO? HojaPedido,
    string? Observacion,
    UsuarioResumenDTO Usuario);
