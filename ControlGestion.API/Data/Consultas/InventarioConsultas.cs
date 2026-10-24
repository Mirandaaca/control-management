using System.Linq.Expressions;
using ControlGestion.API.DTOs;
using ControlGestion.API.Models;

namespace ControlGestion.API.Data.Consultas;

public static class InventarioConsultas
{
    /// <summary>Bajo el mínimo es un stock igual o menor que el mínimo (alcance, sección 6). Sin fila de inventario, stock 0.</summary>
    public static IQueryable<Producto> BajoElMinimo(this IQueryable<Producto> productos) =>
        productos.Where(p => (p.Inventario == null ? 0m : p.Inventario.StockActual) <= (p.Inventario == null ? 0m : p.Inventario.StockMinimo));

    // El inventario se lista desde los productos: así un producto sin fila de inventario aparece con stock 0.
    public static readonly Expression<Func<Producto, InventarioDTO>> ComoInventario = p => new InventarioDTO(
        p.Id,
        p.SKU,
        p.Descripcion,
        p.Categoria,
        p.Precio,
        p.Inventario == null ? 0m : p.Inventario.StockActual,
        p.Inventario == null ? 0m : p.Inventario.StockMinimo,
        (p.Inventario == null ? 0m : p.Inventario.StockActual) <= (p.Inventario == null ? 0m : p.Inventario.StockMinimo),
        p.Inventario == null ? (DateTime?)null : p.Inventario.UltimaActualizacion);
}
