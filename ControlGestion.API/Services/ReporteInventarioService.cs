using ClosedXML.Excel;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Models;
using ControlGestion.API.Services.Pdf;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>
/// El reporte de inventario (6.4, 7.3.25): el stock de los productos activos, los que están en su mínimo o debajo y los
/// movimientos del período, con la categoría como filtro; en Excel y en PDF (tarea 33). El inventario es de la empresa: no
/// tiene cartera.
/// </summary>
public class ReporteInventarioService(AppDbContext db, TimeProvider reloj, GeneradorDePdf pdf)
{
    private const string FormatoFecha = "dd/mm/yyyy hh:mm";

    private static readonly Dictionary<TipoMovimiento, string> Tipos = new()
    {
        [TipoMovimiento.Entrada] = "Entrada", [TipoMovimiento.Salida] = "Salida", [TipoMovimiento.Ajuste] = "Ajuste",
        [TipoMovimiento.DevolucionVenta] = "Devolución"
    };

    // Sin fila de inventario, stock y mínimo 0 (InventarioConsultas): el producto aparece y queda en su mínimo.
    private IQueryable<StockDeProductoDTO> Productos(FiltrosReporteInventarioDTO filtros) =>
        db.Productos.Where(p => p.Activo).DeLaCategoria(filtros.CategoriaId)
            .OrderBy(p => p.Descripcion).ThenBy(p => p.Id)
            .Select(p => new StockDeProductoDTO(p.Id, p.SKU, p.Descripcion, p.Categoria!.Nombre,
                p.Inventario == null ? 0m : p.Inventario.StockActual,
                p.Inventario == null ? 0m : p.Inventario.StockMinimo,
                p.Inventario == null || p.Inventario.StockActual >= p.Inventario.StockMinimo
                    ? 0m : p.Inventario.StockMinimo - p.Inventario.StockActual));

    private IQueryable<MovimientoInventario> Movimientos(FiltrosReporteInventarioDTO filtros)
    {
        var consulta = db.MovimientosInventario.AsQueryable();
        if (filtros.CategoriaId is { } categoriaId) consulta = consulta.Where(m => m.Producto!.CategoriaId == categoriaId);
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
        // Todas las registradas (tarea 29): con una sin productos activos, la tabla dice que no hay.
        var categorias = await db.Categorias.OrderBy(c => c.Nombre).Select(c => new CategoriaResumenDTO(c.Id, c.Nombre)).ToListAsync();
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
        if (bajoElMinimo.Count == 0) bajo.Cell(2, 1).Value = SinBajoElMinimo;
        else Productos(bajo, bajoElMinimo, conFalta: true);

        var hoja = libro.Worksheets.Add("Movimientos");
        Encabezados(hoja, "Fecha", "Producto", "Tipo", "Cantidad", "Stock resultante", "Usuario");
        if (movimientos.Count == 0) hoja.Cell(2, 1).Value = SinMovimientos;
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

    private const string SinBajoElMinimo = "Ningún producto está por debajo de su mínimo.";
    private const string SinMovimientos = "No hay movimientos en el período.";

    /// <summary>
    /// El PDF (tarea 33): el stock, los que están bajo el mínimo y los movimientos, como el Excel. Este reporte no tiene la
    /// regla de "sin datos": una parte vacía lo dice (7.3.25, ext. 3 y 4).
    /// </summary>
    public async Task<(byte[] Contenido, string NombreArchivo)> PdfAsync(FiltrosReporteInventarioDTO filtros)
    {
        var productos = await Productos(filtros).ToListAsync();
        var movimientos = await Movimientos(filtros).Select(ComoDTO).ToListAsync();

        var elegidos = new List<FiltroPdf>();
        if (filtros.CategoriaId is { } categoriaId)
            elegidos.Agregar("Categoría", await db.Categorias.Where(c => c.Id == categoriaId).Select(c => c.Nombre).FirstOrDefaultAsync());
        elegidos.Agregar("Movimientos", TextosDelPdf.Periodo(filtros.Desde, filtros.Hasta));

        ColumnaPdf[] columnas =
        [
            new("Código", 1.3f), new("Producto", 3f), new("Categoría", 1.6f), new("Stock", 1f, Derecha: true),
            new("Mínimo", 1f, Derecha: true)
        ];
        var c = TextosDelPdf.Cantidad;
        var reporte = new ReportePdf("Reporte de inventario", elegidos,
        [
            new SeccionPdf("Stock", columnas,
                productos.Select(p => TextosDelPdf.Fila(p.Codigo, p.Producto, TextosDelPdf.Texto(p.Categoria), c(p.StockActual),
                    c(p.StockMinimo))).ToList(),
                SiVacia: "No hay productos activos en esta categoría."),
            new SeccionPdf("Bajo el mínimo", [.. columnas, new("Falta", 1f, Derecha: true)],
                BajoElMinimo(productos).Select(p => TextosDelPdf.Fila(p.Codigo, p.Producto, TextosDelPdf.Texto(p.Categoria),
                    c(p.StockActual), c(p.StockMinimo), c(p.Falta))).ToList(),
                SiVacia: SinBajoElMinimo),
            new SeccionPdf("Movimientos",
                [
                    new("Fecha", 1.5f), new("Producto", 2.6f), new("Tipo", 1.1f), new("Cantidad", 1f, Derecha: true),
                    new("Stock resultante", 1.3f, Derecha: true), new("Usuario", 2f)
                ],
                movimientos.Select(m => TextosDelPdf.Fila(TextosDelPdf.FechaHora(m.Fecha), m.Producto, Tipos[m.Tipo],
                    c(m.Cantidad), c(m.StockResultante), m.Usuario.NombreCompleto)).ToList(),
                SiVacia: SinMovimientos)
        ]);
        return (pdf.Generar(reporte), $"inventario_{HoraBolivia.Hoy(reloj):yyyyMMdd}.pdf");
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
