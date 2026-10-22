using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

public record GuardarProductoDTO
{
    [Required(ErrorMessage = "El código (SKU) es obligatorio.")]
    [MaxLength(50, ErrorMessage = "El código no puede superar los 50 caracteres.")]
    public string Sku { get; init; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [MaxLength(150, ErrorMessage = "La descripción no puede superar los 150 caracteres.")]
    public string Descripcion { get; init; } = string.Empty;

    [MaxLength(80, ErrorMessage = "La categoría no puede superar los 80 caracteres.")]
    public string? Categoria { get; init; }

    /// <summary>Precio de venta.</summary>
    [Range(0, 99_999_999, MinimumIsExclusive = true, ErrorMessage = "El precio de venta debe ser mayor que cero.")]
    public decimal Precio { get; init; }

    [Range(0, 99_999_999, MinimumIsExclusive = true, ErrorMessage = "El precio de compra debe ser mayor que cero.")]
    public decimal PrecioCompra { get; init; }

    /// <summary>Con el stock igual o menor que este valor, el producto sale "bajo el mínimo".</summary>
    [Range(0, 9_999_999, ErrorMessage = "El stock mínimo no puede ser negativo.")]
    public decimal StockMinimo { get; init; }
}

/// <summary>Filtros de la lista de productos. Buscar compara con el código, la descripción y la categoría.</summary>
public record FiltrosProductoDTO
{
    [MaxLength(100, ErrorMessage = "La búsqueda no puede superar los 100 caracteres.")]
    public string? Buscar { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "La página empieza en 1.")]
    public int Pagina { get; init; } = 1;

    [Range(1, 100, ErrorMessage = "El tamaño de la página va de 1 a 100.")]
    public int Tamano { get; init; } = 25;
}

public record ProductoDTO(
    int Id,
    string Sku,
    string Descripcion,
    string? Categoria,
    decimal Precio,
    decimal PrecioCompra,
    decimal StockMinimo,
    decimal StockActual,
    bool Activo);

/// <summary>Lo justo para elegir un producto en una HP o un pedido: sin el precio de compra.</summary>
public record ProductoOpcionDTO(int Id, string Sku, string Descripcion, string? Categoria, decimal Precio, decimal StockActual);
