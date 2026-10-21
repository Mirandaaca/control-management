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

    /// <summary>
    /// Salida por una venta confirmada (2.5). Revisa el stock de todos los productos antes de tocar alguno: si uno no
    /// alcanza, no se descuenta nada. No guarda: lo hace quien confirma, en su transacción.
    /// </summary>
    public async Task DescontarPorVentaAsync(HojaPedido hp)
    {
        // Una HP puede traer el mismo producto en dos líneas: el stock se compara con la suma.
        var pedidos = hp.Detalles.GroupBy(d => d.ProductoId).ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad));
        var ids = pedidos.Keys.ToList();
        var productos = await db.Productos.Include(p => p.Inventario).Where(p => ids.Contains(p.Id)).ToListAsync();

        foreach (var producto in productos)
        {
            // Sin fila de inventario, el producto nunca recibió mercadería: su stock es 0.
            var stock = producto.Inventario?.StockActual ?? 0m;
            if (stock < pedidos[producto.Id])
                throw new ReglaDeNegocioException("stock_insuficiente",
                    $"No hay stock suficiente de {producto.Descripcion}: hay {stock:0.###} y la HP pide {pedidos[producto.Id]:0.###}.");
        }

        foreach (var producto in productos)
            Mover(producto.Inventario!, -pedidos[producto.Id], TipoMovimiento.Salida, MovimientoInventario.ReferenciaHojaPedido, hp.Id);
    }

    /// <summary>Al anular una HP confirmada vuelve lo que salió, como devolución en el kardex (2.5). No guarda.</summary>
    public async Task DevolverPorAnulacionAsync(HojaPedido hp)
    {
        var devueltos = hp.Detalles.GroupBy(d => d.ProductoId).ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad));
        var ids = devueltos.Keys.ToList();
        var inventarios = await db.InventariosProducto.Where(i => ids.Contains(i.ProductoId)).ToListAsync();
        foreach (var inventario in inventarios)
            Mover(inventario, devueltos[inventario.ProductoId], TipoMovimiento.DevolucionVenta,
                MovimientoInventario.ReferenciaHojaPedido, hp.Id);
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
        Mover(inventario, dto.Cantidad, TipoMovimiento.Entrada, MovimientoInventario.ReferenciaIngreso, 0, dto.Observacion.Trim());

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

    /// <summary>Cambia el stock y deja el movimiento en el kardex: la cantidad va sin signo y el tipo dice si sumó o restó.</summary>
    private void Mover(InventarioProducto inventario, decimal variacion, TipoMovimiento tipo, string referenciaTabla,
        int referenciaId, string? observacion = null)
    {
        var stockAnterior = inventario.StockActual;
        inventario.StockActual += variacion;
        inventario.UltimaActualizacion = Ahora;

        db.MovimientosInventario.Add(new MovimientoInventario
        {
            ProductoId = inventario.ProductoId,
            TipoMovimiento = tipo,
            Cantidad = Math.Abs(variacion),
            StockAnterior = stockAnterior,
            StockResultante = inventario.StockActual,
            ReferenciaTabla = referenciaTabla,
            ReferenciaId = referenciaId,
            UsuarioId = usuario.Id,
            Fecha = Ahora,
            Observacion = observacion
        });
    }

    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;
}
