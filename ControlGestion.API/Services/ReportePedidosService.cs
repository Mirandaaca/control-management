using ClosedXML.Excel;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services.Pdf;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>
/// Reporte de pedidos (1.4): filtros de estado, zona, promotor y días de entrega, totales por estado, Excel y PDF (tarea
/// 33). Respeta la cartera como la lista.
/// </summary>
public class ReportePedidosService(AppDbContext db, IUsuarioActual usuario, TimeProvider reloj, GeneradorDePdf pdf)
{
    private const string FormatoFecha = "dd/mm/yyyy hh:mm";

    private IQueryable<Pedido> Filtrados(FiltrosPedidoDTO filtros) => db.Pedidos.VisiblesPara(usuario).Filtrar(filtros);

    public async Task<ReportePedidosDTO> GenerarAsync(FiltrosPedidoDTO filtros)
    {
        var consulta = Filtrados(filtros);
        var (porEstado, cantidad, monto) = await TotalesAsync(consulta);
        var total = await consulta.CountAsync();
        var items = await consulta
            .OrderByDescending(p => p.FechaEntregaEstimada).ThenByDescending(p => p.Id)
            .Skip((filtros.Pagina - 1) * filtros.Tamano)
            .Take(filtros.Tamano)
            .Select(PedidoConsultas.ComoLista)
            .ToListAsync();
        return new ReportePedidosDTO(porEstado, cantidad, monto, new Pagina<PedidoListaDTO>(items, total, filtros.Pagina, filtros.Tamano));
    }

    private sealed record FilaDelPedido(
        string Numero, string Estado, string Cliente, string Zona, string? Promotor, string? Asignado, DateTime FechaRegistro,
        DateTime? FechaEntregaEstimada, DateTime? FechaEntregaReal, string DireccionEntrega, string? RecibidoPor, string? Hoja,
        decimal Total);

    /// <summary>
    /// Lo que se exporta, a Excel o a PDF: todos los pedidos de los filtros, sin paginar, y los totales. Sin pedidos no se
    /// exporta; un anulado sí cuenta, porque es una fila del archivo.
    /// </summary>
    private async Task<(List<TotalPorEstadoDTO> PorEstado, int Cantidad, decimal Monto, List<FilaDelPedido> Filas)> ParaExportarAsync(
        FiltrosPedidoDTO filtros)
    {
        var consulta = Filtrados(filtros);
        if (!await consulta.AnyAsync())
            throw new ReglaDeNegocioException("reporte_sin_datos", "No hay pedidos con esos filtros: no hay nada que exportar.");
        var (porEstado, cantidad, monto) = await TotalesAsync(consulta);
        var filas = await consulta
            .OrderBy(p => p.FechaEntregaEstimada).ThenBy(p => p.Id)
            .Select(p => new FilaDelPedido(
                p.Numero,
                p.EstadoPedido!.Nombre,
                p.Cliente!.NombreCompleto,
                p.Zona!.Nombre,
                p.Promotor == null ? null : p.Promotor.NombreCompleto,
                p.Repartidor == null ? null : p.Repartidor.NombreCompleto,
                p.FechaRegistro,
                p.FechaEntregaEstimada,
                p.FechaEntregaReal,
                p.DireccionEntrega,
                p.RecibidoPor,
                p.HojaPedido == null ? null : p.HojaPedido.Numero,
                p.Total))
            .ToListAsync();
        return (porEstado, cantidad, monto, filas);
    }

    /// <summary>El Excel del reporte: una hoja con los pedidos y otra con los totales.</summary>
    public async Task<(byte[] Contenido, string NombreArchivo)> ExcelAsync(FiltrosPedidoDTO filtros)
    {
        var (porEstado, cantidad, monto, filas) = await ParaExportarAsync(filtros);

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Pedidos");
        string[] encabezados = ["Número", "Estado", "Cliente", "Zona", "Promotor", "Asignado a", "Registrado", "Entrega",
            "Entregado", "Dirección de entrega", "Recibió", "HP", "Total (Bs)"];
        for (var c = 0; c < encabezados.Length; c++) hoja.Cell(1, c + 1).Value = encabezados[c];
        hoja.Row(1).Style.Font.Bold = true;

        var fila = 2;
        foreach (var p in filas)
        {
            hoja.Cell(fila, 1).Value = p.Numero;
            hoja.Cell(fila, 2).Value = p.Estado;
            hoja.Cell(fila, 3).Value = p.Cliente;
            hoja.Cell(fila, 4).Value = p.Zona;
            hoja.Cell(fila, 5).Value = p.Promotor;
            hoja.Cell(fila, 6).Value = p.Asignado;
            Fecha(hoja.Cell(fila, 7), p.FechaRegistro);
            Fecha(hoja.Cell(fila, 8), p.FechaEntregaEstimada);
            Fecha(hoja.Cell(fila, 9), p.FechaEntregaReal);
            hoja.Cell(fila, 10).Value = p.DireccionEntrega;
            hoja.Cell(fila, 11).Value = p.RecibidoPor;
            hoja.Cell(fila, 12).Value = p.Hoja;
            hoja.Cell(fila, 13).Value = p.Total;
            hoja.Cell(fila, 13).Style.NumberFormat.Format = "#,##0.00";
            fila++;
        }
        hoja.Columns().AdjustToContents();

        var totales = libro.Worksheets.Add("Totales por estado");
        totales.Cell(1, 1).Value = "Estado";
        totales.Cell(1, 2).Value = "Pedidos";
        totales.Cell(1, 3).Value = "Monto (Bs)";
        totales.Row(1).Style.Font.Bold = true;
        var t = 2;
        foreach (var e in porEstado)
        {
            totales.Cell(t, 1).Value = e.Estado;
            totales.Cell(t, 2).Value = e.Cantidad;
            totales.Cell(t, 3).Value = e.Monto;
            t++;
        }
        totales.Cell(t, 1).Value = "Total sin anulados";
        totales.Cell(t, 2).Value = cantidad;
        totales.Cell(t, 3).Value = monto;
        totales.Row(t).Style.Font.Bold = true;
        totales.Column(3).Style.NumberFormat.Format = "#,##0.00";
        totales.Columns().AdjustToContents();

        using var salida = new MemoryStream();
        libro.SaveAs(salida);
        return (salida.ToArray(), $"pedidos_{HoraBolivia.Hoy(reloj):yyyyMMdd}.xlsx");
    }

    /// <summary>
    /// El PDF del reporte (tarea 33): lo mismo que el Excel, con las columnas de la tabla del panel. La hoja va de costado.
    /// </summary>
    public async Task<(byte[] Contenido, string NombreArchivo)> PdfAsync(FiltrosPedidoDTO filtros)
    {
        var (porEstado, cantidad, monto, filas) = await ParaExportarAsync(filtros);

        var elegidos = new List<FiltroPdf>();
        if (filtros.EstadoId is { } estadoId)
            elegidos.Agregar("Estado", await db.EstadosPedido.Where(e => e.Id == estadoId).Select(e => e.Nombre).FirstOrDefaultAsync());
        if (filtros.ZonaId is { } zonaId) elegidos.Agregar("Zona", await db.NombreDeZonaAsync(zonaId));
        if (!string.IsNullOrEmpty(filtros.PromotorId)) elegidos.Agregar("Promotor", await db.NombreDeUsuarioAsync(filtros.PromotorId));
        if (!string.IsNullOrEmpty(filtros.RepartidorId)) elegidos.Agregar("Asignado a", await db.NombreDeUsuarioAsync(filtros.RepartidorId));
        if (filtros.SinAsignar) elegidos.Agregar("Asignado a", "Sin asignar");
        if (filtros.ClienteId is { } clienteId) elegidos.Agregar("Cliente", await db.NombreDeClienteAsync(clienteId, usuario, reloj));
        elegidos.Agregar("Búsqueda", filtros.Buscar?.Trim());
        elegidos.Agregar("Entrega", TextosDelPdf.Periodo(filtros.Desde, filtros.Hasta));

        var reporte = new ReportePdf("Reporte de pedidos", elegidos,
        [
            new SeccionPdf("Totales por estado",
                [new ColumnaPdf("Estado", 3), new ColumnaPdf("Pedidos", 1, Derecha: true), new ColumnaPdf("Monto", 1.5f, Derecha: true)],
                porEstado.Select(e => TextosDelPdf.Fila(e.Estado, TextosDelPdf.Cantidad(e.Cantidad), TextosDelPdf.Monto(e.Monto))).ToList(),
                Total: ["Total sin anulados", TextosDelPdf.Cantidad(cantidad), TextosDelPdf.Monto(monto)],
                AnchoMaximo: AnchosPdf.Totales),
            new SeccionPdf("Pedidos",
                [
                    new ColumnaPdf("Número", 1.6f), new ColumnaPdf("Cliente", 2.2f), new ColumnaPdf("Zona", 1.4f),
                    new ColumnaPdf("Promotor", 1.8f), new ColumnaPdf("Entrega", 1.5f), new ColumnaPdf("Asignado a", 1.8f),
                    new ColumnaPdf("Total", 1.2f, Derecha: true), new ColumnaPdf("Estado", 1f)
                ],
                filas.Select(p => TextosDelPdf.Fila(p.Numero, p.Cliente, p.Zona, TextosDelPdf.Texto(p.Promotor),
                    TextosDelPdf.FechaHora(p.FechaEntregaEstimada), TextosDelPdf.Texto(p.Asignado), TextosDelPdf.Monto(p.Total),
                    p.Estado)).ToList())
        ], Horizontal: true);
        return (pdf.Generar(reporte), $"pedidos_{HoraBolivia.Hoy(reloj):yyyyMMdd}.pdf");
    }

    /// <summary>
    /// Los cuatro estados, también los que no tienen pedidos. Los anulados se ven en su fila, pero no suman al total:
    /// ese pedido ya no se entrega ni se cobra.
    /// </summary>
    private async Task<(List<TotalPorEstadoDTO> PorEstado, int Cantidad, decimal Monto)> TotalesAsync(IQueryable<Pedido> consulta)
    {
        var agrupados = await consulta
            .GroupBy(p => p.EstadoPedidoId)
            .Select(g => new { EstadoId = g.Key, Cantidad = g.Count(), Monto = g.Sum(p => p.Total) })
            .ToDictionaryAsync(g => g.EstadoId);
        var estados = await db.EstadosPedido.OrderBy(e => e.Orden).Select(e => new { e.Id, e.Nombre }).ToListAsync();

        var porEstado = estados
            .Select(e => agrupados.TryGetValue(e.Id, out var g)
                ? new TotalPorEstadoDTO(e.Id, e.Nombre, g.Cantidad, g.Monto)
                : new TotalPorEstadoDTO(e.Id, e.Nombre, 0, 0m))
            .ToList();
        var vigentes = porEstado.Where(e => e.EstadoId != EstadosDePedido.Anulado).ToList();
        return (porEstado, vigentes.Sum(e => e.Cantidad), vigentes.Sum(e => e.Monto));
    }

    // En el Excel las fechas van en la hora de Bolivia, no en la del servidor (ToLocalTime en ReportesController).
    private static void Fecha(IXLCell celda, DateTime? utc)
    {
        if (utc is not { } valor) return;
        celda.Value = HoraBolivia.DesdeUtc(valor);
        celda.Style.DateFormat.Format = FormatoFecha;
    }
}
