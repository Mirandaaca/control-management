using System.Linq.Expressions;
using ControlGestion.API.DTOs;
using ControlGestion.API.Models;

namespace ControlGestion.API.Data.Consultas;

public static class ProductoConsultas
{
    /// <summary>
    /// Busca en el código, la descripción y el nombre de la categoría, sin distinguir mayúsculas ni tildes (intercalación
    /// de la base).
    /// </summary>
    public static IQueryable<Producto> Buscar(this IQueryable<Producto> productos, string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return productos;
        var t = texto.Trim();
        return productos.Where(p => p.SKU.Contains(t) || p.Descripcion.Contains(t) || p.Categoria!.Nombre.Contains(t));
    }

    /// <summary>Solo los de esa categoría; sin ella, todos.</summary>
    public static IQueryable<Producto> DeLaCategoria(this IQueryable<Producto> productos, int? categoriaId) =>
        categoriaId is { } id ? productos.Where(p => p.CategoriaId == id) : productos;

    // Un producto dado de alta antes de que el alta creara su inventario no tiene fila: cuenta como stock 0.
    public static readonly Expression<Func<Producto, ProductoDTO>> ComoDTO = p => new ProductoDTO(
        p.Id,
        p.SKU,
        p.Descripcion,
        new CategoriaResumenDTO(p.CategoriaId, p.Categoria!.Nombre),
        p.Precio,
        p.PrecioCompra,
        p.Inventario == null ? 0m : p.Inventario.StockMinimo,
        p.Inventario == null ? 0m : p.Inventario.StockActual,
        p.Activo);

    public static readonly Expression<Func<Producto, ProductoOpcionDTO>> ComoOpcion = p => new ProductoOpcionDTO(
        p.Id,
        p.SKU,
        p.Descripcion,
        p.Categoria!.Nombre,
        p.Precio,
        p.Inventario == null ? 0m : p.Inventario.StockActual);
}
