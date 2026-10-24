using ClosedXML.Excel;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.DTOs;
using ControlGestion.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>
/// El reporte de inventario (6.4, 7.3.25): el stock de los productos activos, los que están en su mínimo o debajo y los
/// movimientos del período, con la categoría como filtro. El inventario es de la empresa: no tiene cartera.
/// </summary>
public class ReporteInventarioService(AppDbContext db, TimeProvider reloj)
{
    private const string FormatoFecha = "dd/mm/yyyy hh:mm";

    private static readonly Dictionary<TipoMovimiento, string> Tipos = new()
    {
        [TipoMovimiento.Entrada] = "Entrada", [TipoMovimiento.Salida] = "Salida", [TipoMovimiento.Ajuste] = "Ajuste",
        [TipoMovimiento.DevolucionVenta] = "Devolución"
    };

    // Sin fila de inventario, stock y mínimo 0 (InventarioConsultas): el producto aparece y queda en su mínimo.
    private IQueryable<StockDeProductoDTO> Productos(FiltrosReporteInventarioDTO filtros)
    {
        var consulta = db.Productos.Where(p => p.Activo);
        if (!string.IsNullOrEmpty(filtros.Categoria)) consulta = consulta.Where(p => p.Categoria == filtros.Categoria);
        return consulta
            .OrderBy(p => p.Descripcion).ThenBy(p => p.Id)
            .Select(p => new StockDeProductoDTO(p.Id, p.SKU, p.Descripcion, p.Categoria,
                p.Inventario == null ? 0m : p.Inventario.StockActual,
                p.Inventario == null ? 0m : p.Inventario.StockMinimo,
                p.Inventario == null || p.Inventario.StockActual >= p.Inventario.StockMinimo
                    ? 0m : p.Inventario.StockMinimo - p.Inventario.StockActual));
    }

    private IQueryable<MovimientoInventario> Movimientos(FiltrosReporteInventarioDTO filtros)
    {
        var consulta = db.MovimientosInventario.AsQueryable();
        if (!string.IsNullOrEmpty(filtros.Categoria)) consulta = consulta.Where(m => m.Producto!.Categoria == filtros.Categoria);
        if (filtros.Desde is { } desde)
        {
            var inicio = HoraBolivia.InicioDelDiaUtc(desde);
            consulta = consulta.Where(m => m.Fecha >= inicio);
        }
        if (filtros.Hasta is { } hasta)
        {
            var fin = HoraBolivia.FinDelDiaUtc(hasta);
            consulta = consulta.Where(m => m.Fecha < fin);
        }
        return consulta.OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id);
    }

    private static readonly System.Linq.Expressions.Expression<Func<MovimientoInventario, MovimientoDelReporteDTO>> ComoDTO =
        m => new MovimientoDelReporteDTO(m.Id, m.Fecha, m.Producto!.Descripcion, m.TipoMovimiento, m.Cantidad, m.StockResultante,
            new UsuarioResumenDTO(m.Usuario!.Id, m.Usuario.NombreCompleto));

    // Bajo el mínimo es un stock igual o menor que el mínimo (alcance, sección 6).
    private static List<StockDeProductoDTO> BajoElMinimo(List<StockDeProductoDTO> productos) =>
        productos.Where(p => p.StockActual <= p.StockMinimo).ToList();

    public async Task<ReporteInventarioDTO> GenerarAsync(FiltrosReporteInventarioDTO filtros)
    {
        var categorias = await db.Productos
            .Where(p => p.Activo && p.Categoria != null && p.Categoria != "")
            .Select(p => p.Categoria!).Distinct().OrderBy(c => c).ToListAsync();
        var productos = await Productos(filtros).ToListAsync();
        var movimientos = Movimientos(filtros);
        var total = await movimientos.CountAsync();
        var pagina = await movimientos.Skip((filtros.Pagina - 1) * filtros.Tamano).Take(filtros.Tamano).Select(ComoDTO).ToListAsync();
        return new ReporteInventarioDTO(categorias, productos, BajoElMinimo(productos),
            new Pagina<MovimientoDelReporteDTO>(pagina, total, filtros.Pagina, filtros.Tamano));
    }

    /// <summary>El stock, los productos bajo el mínimo y todos los movimientos del período, cada uno en su hoja.</summary>
    public async Task<(byte[] Contenido, string NombreArchivo)> ExcelAsync(FiltrosReporteInventarioDTO filtros)
    {
        var productos = await Productos(filtros).ToListAsync();
        var movimientos = await Movimientos(filtros).Select(ComoDTO).ToListAsync();

        using var libro = new XLWorkbook();
        var stock = libro.Worksheets.Add("Stock");
        Encabezados(stock, "Código", "Producto", "Categoría", "Stock", "Mínimo");
        Productos(stock, productos, conFalta: false);

        var bajo = libro.Worksheets.Add("Bajo el mínimo");
        Encabezados(bajo, "Código", "Producto", "Categoría", "Stock", "Mínimo", "Falta");
        var bajoElMinimo = BajoElMinimo(productos);
        if (bajoElMinimo.Count == 0) bajo.Cell(2, 1).Value = "Ningún producto está por debajo de su mínimo.";
        else Productos(bajo, bajoElMinimo, conFalta: true);

        var hoja = libro.Worksheets.Add("Movimientos");
        Encabezados(hoja, "Fecha", "Producto", "Tipo", "Cantidad", "Stock resultante", "Usuario");
        if (movimientos.Count == 0) hoja.Cell(2, 1).Value = "No hay movimientos en el período.";
        var fila = 2;
        foreach (var m in movimientos)
        {
            hoja.Cell(fila, 1).Value = HoraBolivia.DesdeUtc(m.Fecha);
            hoja.Cell(fila, 1).Style.DateFormat.Format = FormatoFecha;
            hoja.Cell(fila, 2).Value = m.Producto;
            hoja.Cell(fila, 3).Value = Tipos[m.Tipo];
            hoja.Cell(fila, 4).Value = m.Cantidad;
            hoja.Cell(fila, 5).Value = m.StockResultante;
            hoja.Cell(fila, 6).Value = m.Usuario.NombreCompleto;
            fila++;
        }
        foreach (var h in libro.Worksheets) h.Columns().AdjustToContents();

        using var salida = new MemoryStream();
        libro.SaveAs(salida);
        return (salida.ToArray(), $"inventario_{HoraBolivia.Hoy(reloj):yyyyMMdd}.xlsx");
    }

    private static void Encabezados(IXLWorksheet hoja, params string[] encabezados)
    {
        for (var c = 0; c < encabezados.Length; c++) hoja.Cell(1, c + 1).Value = encabezados[c];
        hoja.Row(1).Style.Font.Bold = true;
    }

    private static void Productos(IXLWorksheet hoja, List<StockDeProductoDTO> productos, bool conFalta)
    {
        var fila = 2;
        foreach (var p in productos)
        {
            hoja.Cell(fila, 1).Value = p.Codigo;
            hoja.Cell(fila, 2).Value = p.Producto;
            hoja.Cell(fila, 3).Value = p.Categoria;
            hoja.Cell(fila, 4).Value = p.StockActual;
            hoja.Cell(fila, 5).Value = p.StockMinimo;
            if (conFalta) hoja.Cell(fila, 6).Value = p.Falta;
            fila++;
        }
    }
}
