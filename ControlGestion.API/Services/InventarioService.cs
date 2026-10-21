using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>Stock de cada producto y su kardex (2.5).</summary>
public class InventarioService(AppDbContext db, IUsuarioActual usuario, TimeProvider reloj)
{
    public async Task<Pagina<InventarioDTO>> ListarAsync(FiltrosInventarioDTO filtros)
    {
        var consulta = db.Productos.Where(p => p.Activo).Buscar(filtros.Buscar);
        if (filtros.SoloBajoMinimo) consulta = consulta.BajoElMinimo();

        var total = await consulta.CountAsync();
        var items = await consulta
            .OrderBy(p => p.Descripcion).ThenBy(p => p.Id)
            .Skip((filtros.Pagina - 1) * filtros.Tamano)
            .Take(filtros.Tamano)
            .Select(InventarioConsultas.ComoInventario)
            .ToListAsync();
        return new Pagina<InventarioDTO>(items, total, filtros.Pagina, filtros.Tamano);
    }

    public async Task<InventarioDTO> ObtenerAsync(int productoId) =>
        await db.Productos.Where(p => p.Id == productoId).Select(InventarioConsultas.ComoInventario).FirstOrDefaultAsync()
        ?? throw new NoEncontradoException("No se encontró el producto.");

    /// <summary>Los movimientos de un producto, del más reciente al más antiguo.</summary>
    public async Task<Pagina<MovimientoDTO>> KardexAsync(int productoId, FiltrosKardexDTO filtros)
    {
        if (!await db.Productos.AnyAsync(p => p.Id == productoId))
            throw new NoEncontradoException("No se encontró el producto.");

        var consulta = db.MovimientosInventario.Where(m => m.ProductoId == productoId);
        var total = await consulta.CountAsync();
        var items = await consulta
            .OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id)
            .Skip((filtros.Pagina - 1) * filtros.Tamano)
            .Take(filtros.Tamano)
            .Select(m => new MovimientoDTO(
                m.Id,
                m.Fecha,
                m.TipoMovimiento,
                m.Cantidad,
                // El signo sale del stock y no del tipo: los ajustes viejos guardaban la cantidad sin signo.
                m.StockResultante - m.StockAnterior,
                m.StockAnterior,
                m.StockResultante,
                m.ReferenciaTabla == MovimientoInventario.ReferenciaHojaPedido
                    ? db.HojasPedido.Where(h => h.Id == m.ReferenciaId).Select(h => new HojaPedidoResumenDTO(h.Id, h.Numero)).FirstOrDefault()
                    : null,
                m.Observacion,
                new UsuarioResumenDTO(m.Usuario!.Id, m.Usuario.NombreCompleto)))
            .ToListAsync();
        return new Pagina<MovimientoDTO>(items, total, filtros.Pagina, filtros.Tamano);
    }

    public async Task DescontarStockPorHP(HojaPedido hp, string usuarioId)
    {
        foreach (var detalle in hp.Detalles)
        {
            var inventario = await db.InventariosProducto
                .FirstOrDefaultAsync(i => i.ProductoId == detalle.ProductoId);

            if (inventario == null)
            {
                inventario = new InventarioProducto
                {
                    ProductoId = detalle.ProductoId,
                    StockActual = 0
                };
                db.InventariosProducto.Add(inventario);
                await db.SaveChangesAsync();
            }

            var stockAnterior = inventario.StockActual;
            inventario.StockActual -= detalle.Cantidad;
            inventario.UltimaActualizacion = Ahora;

            db.MovimientosInventario.Add(new MovimientoInventario
            {
                ProductoId = detalle.ProductoId,
                TipoMovimiento = TipoMovimiento.Salida,
                Cantidad = detalle.Cantidad,
                StockAnterior = stockAnterior,
                StockResultante = inventario.StockActual,
                ReferenciaTabla = MovimientoInventario.ReferenciaHojaPedido,
                ReferenciaId = hp.Id,
                UsuarioId = usuarioId,
                Fecha = Ahora
            });
        }
    }

    /// <summary>Ingreso de mercadería: suma al stock y queda en el kardex como entrada.</summary>
    public async Task<InventarioDTO> RegistrarIngresoAsync(int productoId, RegistrarIngresoDTO dto)
    {
        var activo = await db.Productos.Where(p => p.Id == productoId).Select(p => (bool?)p.Activo).FirstOrDefaultAsync()
            ?? throw new NoEncontradoException("No se encontró el producto.");
        if (!activo)
            throw new ReglaDeNegocioException("producto_inactivo", "El producto está inactivo: no recibe mercadería.");

        var inventario = await db.InventariosProducto.FirstOrDefaultAsync(i => i.ProductoId == productoId);
        if (inventario == null)
        {
            // Productos dados de alta antes de que el alta creara su fila de inventario.
            inventario = new InventarioProducto { ProductoId = productoId };
            db.InventariosProducto.Add(inventario);
        }

        var stockAnterior = inventario.StockActual;
        inventario.StockActual += dto.Cantidad;
        inventario.UltimaActualizacion = Ahora;

        db.MovimientosInventario.Add(new MovimientoInventario
        {
            ProductoId = productoId,
            TipoMovimiento = TipoMovimiento.Entrada,
            Cantidad = dto.Cantidad,
            StockAnterior = stockAnterior,
            StockResultante = inventario.StockActual,
            ReferenciaTabla = MovimientoInventario.ReferenciaIngreso,
            ReferenciaId = 0,
            UsuarioId = usuario.Id,
            Fecha = Ahora,
            Observacion = dto.Observacion.Trim()
        });

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictoException("conflicto_concurrencia",
                "El stock de este producto cambió mientras registrabas el ingreso. Vuelve a intentarlo.");
        }
        return await ObtenerAsync(productoId);
    }

    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;
}
