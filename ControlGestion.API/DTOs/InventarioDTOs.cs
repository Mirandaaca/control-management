using System.ComponentModel.DataAnnotations;
using ControlGestion.API.Models;

namespace ControlGestion.API.DTOs;

public record AjusteStockDTO(decimal Cantidad, string Observacion);

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
