using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>Catálogo de productos (7.3). Eliminar un producto lo deja inactivo: sigue en las HP y en el kardex.</summary>
public class ProductoService(AppDbContext db, TimeProvider reloj)
{
    public async Task<Pagina<ProductoDTO>> ListarAsync(FiltrosProductoDTO filtros)
    {
        var consulta = db.Productos.Where(p => p.Activo).Buscar(filtros.Buscar);
        var total = await consulta.CountAsync();
        var items = await consulta
            .OrderBy(p => p.Descripcion).ThenBy(p => p.Id)
            .Skip((filtros.Pagina - 1) * filtros.Tamano)
            .Take(filtros.Tamano)
            .Select(ProductoConsultas.ComoDTO)
            .ToListAsync();
        return new Pagina<ProductoDTO>(items, total, filtros.Pagina, filtros.Tamano);
    }

    /// <summary>Para elegir productos en una HP o un pedido: los activos, con su precio de venta y su stock.</summary>
    public Task<List<ProductoOpcionDTO>> OpcionesAsync(string? buscar) =>
        db.Productos.Where(p => p.Activo).Buscar(buscar)
            .OrderBy(p => p.Descripcion).Select(ProductoConsultas.ComoOpcion).ToListAsync();

    public async Task<ProductoDTO> ObtenerAsync(int id) =>
        await db.Productos.Where(p => p.Id == id).Select(ProductoConsultas.ComoDTO).FirstOrDefaultAsync()
        ?? throw new NoEncontradoException("No se encontró el producto.");

    public async Task<ProductoDTO> CrearAsync(GuardarProductoDTO dto)
    {
        await ExigirSkuLibreAsync(dto.Sku, idPropio: null);
        var producto = new Producto();
        Copiar(dto, producto);
        // Sin su fila de inventario, el producto no aparecería en el inventario ni recibiría ingresos.
        producto.Inventario = new InventarioProducto { StockMinimo = dto.StockMinimo, UltimaActualizacion = Ahora };
        db.Productos.Add(producto);
        await db.SaveChangesAsync();
        return await ObtenerAsync(producto.Id);
    }

    public async Task<ProductoDTO> EditarAsync(int id, GuardarProductoDTO dto)
    {
        var producto = await db.Productos.Include(p => p.Inventario).FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NoEncontradoException("No se encontró el producto.");
        await ExigirSkuLibreAsync(dto.Sku, idPropio: id);
        Copiar(dto, producto);
        producto.Inventario ??= new InventarioProducto { UltimaActualizacion = Ahora };
        producto.Inventario.StockMinimo = dto.StockMinimo;
        await db.SaveChangesAsync();
        return await ObtenerAsync(id);
    }

    public async Task DesactivarAsync(int id)
    {
        var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NoEncontradoException("No se encontró el producto.");
        producto.Activo = false;
        await db.SaveChangesAsync();
    }

    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;

    // El índice único también lo frena (409 duplicado), pero así el mensaje dice cuál código se repite.
    private async Task ExigirSkuLibreAsync(string sku, int? idPropio)
    {
        var codigo = sku.Trim();
        if (await db.Productos.AnyAsync(p => p.SKU == codigo && p.Id != idPropio))
            throw new ConflictoException("sku_repetido", $"Ya existe un producto con el código {codigo}.");
    }

    private static void Copiar(GuardarProductoDTO dto, Producto producto)
    {
        producto.SKU = dto.Sku.Trim();
        producto.Descripcion = dto.Descripcion.Trim();
        producto.Categoria = string.IsNullOrWhiteSpace(dto.Categoria) ? null : dto.Categoria.Trim();
        producto.Precio = dto.Precio;
        producto.PrecioCompra = dto.PrecioCompra;
    }
}
