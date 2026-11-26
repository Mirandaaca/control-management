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
/// Reporte de incidencias (4.4): filtros de tipo, estado, promotor, zona y fechas, totales por tipo y por estado, Excel y
/// PDF (tarea 33). Respeta la cartera como la lista.
/// </summary>
public class ReporteEventosService(AppDbContext db, IUsuarioActual usuario, TimeProvider reloj, GeneradorDePdf pdf)
{
    private const string FormatoFecha = "dd/mm/yyyy hh:mm";

    private static readonly (EstadoEvento Estado, string Nombre)[] Estados =
        [(EstadoEvento.Pendiente, "Pendiente"), (EstadoEvento.Resuelto, "Resuelto")];

    // Como en el panel: la prioridad es baja, media o alta (el enum dice Bajo, Medio y Alto).
    private static readonly Dictionary<NivelPrioridad, string> Prioridades = new()
    {
        [NivelPrioridad.Bajo] = "Baja", [NivelPrioridad.Medio] = "Media", [NivelPrioridad.Alto] = "Alta"
    };

    private IQueryable<Evento> Filtrados(FiltrosEventoDTO filtros) => db.Eventos.VisiblesPara(usuario).Filtrar(filtros);

    public async Task<ReporteEventosDTO> GenerarAsync(FiltrosEventoDTO filtros)
    {
        var consulta = Filtrados(filtros);
        var (porTipo, porEstado, cantidad) = await TotalesAsync(consulta);
        var items = await consulta
            .OrderByDescending(e => e.FechaEvento).ThenByDescending(e => e.Id)
            .Skip((filtros.Pagina - 1) * filtros.Tamano)
            .Take(filtros.Tamano)
            .Select(EventoConsultas.ComoLista)
            .ToListAsync();
        return new ReporteEventosDTO(porTipo, porEstado, cantidad, new Pagina<EventoListaDTO>(items, cantidad, filtros.Pagina, filtros.Tamano));
    }

    private sealed record FilaDelEvento(
        string Numero, DateTime FechaEvento, string Tipo, NivelPrioridad NivelPrioridad, EstadoEvento EstadoEvento,
        string? Cliente, string? Pedido, string? Zona, string Registro, string Descripcion, int Fotos, string? Solucion,
        string? ResueltoPor, DateTime? FechaResolucion, decimal Latitud, decimal Longitud);

    /// <summary>
    /// Lo que se exporta, a Excel o a PDF: todos los eventos de los filtros, sin paginar, y los totales. Sin eventos no se
    /// exporta (7.3.16).
    /// </summary>
    private async Task<(List<TotalPorTipoDTO> PorTipo, List<TotalPorEstadoEventoDTO> PorEstado, int Cantidad, List<FilaDelEvento> Filas)>
        ParaExportarAsync(FiltrosEventoDTO filtros)
    {
        var consulta = Filtrados(filtros);
        var (porTipo, porEstado, cantidad) = await TotalesAsync(consulta);
        if (cantidad == 0)
            throw new ReglaDeNegocioException("reporte_sin_datos", "No hay eventos con esos filtros: no hay nada que exportar.");

        var filas = await consulta
            .OrderBy(e => e.FechaEvento).ThenBy(e => e.Id)
            .Select(e => new FilaDelEvento(
                e.Numero,
                e.FechaEvento,
                e.TipoEvento!.Nombre,
                e.TipoEvento.NivelPrioridad,
                e.EstadoEvento,
                e.Cliente == null ? null : e.Cliente.NombreCompleto,
                e.Pedido == null ? null : e.Pedido.Numero,
                e.Cliente == null ? null : e.Cliente.Zona!.Nombre,
                e.Promotor!.NombreCompleto,
                e.Descripcion,
                e.Evidencias.Count,
                e.Solucion,
                e.ResueltoPor == null ? null : e.ResueltoPor.NombreCompleto,
                e.FechaResolucion,
                e.Latitud,
                e.Longitud))
            .ToListAsync();
        return (porTipo, porEstado, cantidad, filas);
    }

    /// <summary>El Excel del reporte: una hoja con los eventos y otra con los totales.</summary>
    public async Task<(byte[] Contenido, string NombreArchivo)> ExcelAsync(FiltrosEventoDTO filtros)
    {
        var (porTipo, porEstado, cantidad, filas) = await ParaExportarAsync(filtros);

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Eventos");
        string[] encabezados = ["Número", "Fecha", "Tipo", "Prioridad", "Estado", "Cliente", "Pedido", "Zona", "Registró",
            "Descripción", "Fotos", "Solución", "Resuelto por", "Resuelto el", "Latitud", "Longitud"];
        for (var c = 0; c < encabezados.Length; c++) hoja.Cell(1, c + 1).Value = encabezados[c];
        hoja.Row(1).Style.Font.Bold = true;

        var fila = 2;
        foreach (var e in filas)
        {
            hoja.Cell(fila, 1).Value = e.Numero;
            Fecha(hoja.Cell(fila, 2), e.FechaEvento);
            hoja.Cell(fila, 3).Value = e.Tipo;
            hoja.Cell(fila, 4).Value = Prioridades[e.NivelPrioridad];
            hoja.Cell(fila, 5).Value = NombreDe(e.EstadoEvento);
            hoja.Cell(fila, 6).Value = e.Cliente;
            hoja.Cell(fila, 7).Value = e.Pedido;
            hoja.Cell(fila, 8).Value = e.Zona;
            hoja.Cell(fila, 9).Value = e.Registro;
            hoja.Cell(fila, 10).Value = e.Descripcion;
            hoja.Cell(fila, 11).Value = e.Fotos;
            hoja.Cell(fila, 12).Value = e.Solucion;
            hoja.Cell(fila, 13).Value = e.ResueltoPor;
            Fecha(hoja.Cell(fila, 14), e.FechaResolucion);
            hoja.Cell(fila, 15).Value = e.Latitud;
            hoja.Cell(fila, 16).Value = e.Longitud;
            fila++;
        }
        hoja.Columns().AdjustToContents();
        // La descripción y la solución pueden ser largas: se cortan en la columna y pasan de línea.
        foreach (var columna in new[] { 10, 12 })
        {
            hoja.Column(columna).Width = 60;
            hoja.Column(columna).Style.Alignment.WrapText = true;
        }

        var totales = libro.Worksheets.Add("Totales");
        totales.Cell(1, 1).Value = "Tipo";
        totales.Cell(1, 2).Value = "Eventos";
        totales.Row(1).Style.Font.Bold = true;
        var t = 2;
        foreach (var tipo in porTipo)
        {
            totales.Cell(t, 1).Value = tipo.Tipo;
            totales.Cell(t, 2).Value = tipo.Cantidad;
            t++;
        }
        t++;
        totales.Cell(t, 1).Value = "Estado";
        totales.Cell(t, 2).Value = "Eventos";
        totales.Row(t).Style.Font.Bold = true;
        t++;
        foreach (var estado in porEstado)
        {
            totales.Cell(t, 1).Value = estado.Nombre;
            totales.Cell(t, 2).Value = estado.Cantidad;
            t++;
        }
        totales.Cell(t, 1).Value = "Total";
        totales.Cell(t, 2).Value = cantidad;
        totales.Row(t).Style.Font.Bold = true;
        totales.Columns().AdjustToContents();

        using var salida = new MemoryStream();
        libro.SaveAs(salida);
        return (salida.ToArray(), $"incidencias_{HoraBolivia.Hoy(reloj):yyyyMMdd}.xlsx");
    }

    /// <summary>
    /// El PDF del reporte (tarea 33): los totales y los eventos, con las columnas de la tabla del panel (las 16 del Excel
    /// no caben en una hoja). La hoja va de costado.
    /// </summary>
    public async Task<(byte[] Contenido, string NombreArchivo)> PdfAsync(FiltrosEventoDTO filtros)
    {
        var (porTipo, porEstado, cantidad, filas) = await ParaExportarAsync(filtros);

        var elegidos = new List<FiltroPdf>();
        if (filtros.TipoEventoId is { } tipoId)
            elegidos.Agregar("Tipo", await db.TiposEvento.Where(t => t.Id == tipoId).Select(t => t.Nombre).FirstOrDefaultAsync());
        if (filtros.Estado is { } estado) elegidos.Agregar("Estado", NombreDe(estado));
        if (!string.IsNullOrEmpty(filtros.PromotorId)) elegidos.Agregar("Registró", await db.NombreDeUsuarioAsync(filtros.PromotorId));
        if (filtros.ZonaId is { } zonaId) elegidos.Agregar("Zona", await db.NombreDeZonaAsync(zonaId));
        if (filtros.ClienteId is { } clienteId) elegidos.Agregar("Cliente", await db.NombreDeClienteAsync(clienteId, usuario, reloj));
        elegidos.Agregar("Búsqueda", filtros.Buscar?.Trim());
        elegidos.Agregar("Fecha", TextosDelPdf.Periodo(filtros.Desde, filtros.Hasta));

        ColumnaPdf[] deTotales = [new("Tipo", 3), new("Eventos", 1, Derecha: true)];
        var reporte = new ReportePdf("Reporte de incidencias", elegidos,
        [
            new SeccionPdf("Totales por tipo", deTotales,
                porTipo.Select(t => TextosDelPdf.Fila(t.Tipo, TextosDelPdf.Cantidad(t.Cantidad))).ToList(),
                AnchoMaximo: AnchosPdf.Totales),
            new SeccionPdf("Totales por estado", [new("Estado", 3), new("Eventos", 1, Derecha: true)],
                porEstado.Select(e => TextosDelPdf.Fila(e.Nombre, TextosDelPdf.Cantidad(e.Cantidad))).ToList(),
                Total: ["Total", TextosDelPdf.Cantidad(cantidad)],
                AnchoMaximo: AnchosPdf.Totales),
            new SeccionPdf("Eventos",
                [
                    new ColumnaPdf("Número", 1.5f), new ColumnaPdf("Fecha", 1.5f), new ColumnaPdf("Tipo", 1.6f),
                    new ColumnaPdf("Cliente", 2.1f), new ColumnaPdf("Zona", 1.4f), new ColumnaPdf("Registró", 1.8f),
                    new ColumnaPdf("Fotos", 0.7f, Derecha: true), new ColumnaPdf("Estado", 1f)
                ],
                filas.Select(e => TextosDelPdf.Fila(e.Numero, TextosDelPdf.FechaHora(e.FechaEvento), e.Tipo,
                    TextosDelPdf.Texto(e.Cliente ?? (e.Pedido is { } pedido ? $"Pedido {pedido}" : null)), TextosDelPdf.Texto(e.Zona),
                    e.Registro, TextosDelPdf.Cantidad(e.Fotos), NombreDe(e.EstadoEvento))).ToList())
        ], Horizontal: true);
        return (pdf.Generar(reporte), $"incidencias_{HoraBolivia.Hoy(reloj):yyyyMMdd}.pdf");
    }

    /// <summary>
    /// Cuántos eventos hay de cada tipo y en cada estado. Salen los tipos activos y los dos estados, también en cero; un
    /// tipo inactivo (tarea 32), solo si tiene eventos en el reporte.
    /// </summary>
    private async Task<(List<TotalPorTipoDTO> PorTipo, List<TotalPorEstadoEventoDTO> PorEstado, int Cantidad)> TotalesAsync(
        IQueryable<Evento> consulta)
    {
        var porTipoId = await consulta.GroupBy(e => e.TipoEventoId)
            .Select(g => new { TipoId = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.TipoId, g => g.Cantidad);
        var porEstadoId = await consulta.GroupBy(e => e.EstadoEvento)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.Estado, g => g.Cantidad);
        var tipos = await db.TiposEvento.OrderBy(t => t.Id).Select(t => new { t.Id, t.Nombre, t.Activo }).ToListAsync();

        var porTipo = tipos.Where(t => t.Activo || porTipoId.ContainsKey(t.Id))
            .Select(t => new TotalPorTipoDTO(t.Id, t.Nombre, porTipoId.GetValueOrDefault(t.Id))).ToList();
        var porEstado = Estados.Select(e => new TotalPorEstadoEventoDTO(e.Estado, e.Nombre, porEstadoId.GetValueOrDefault(e.Estado)))
            .ToList();
        return (porTipo, porEstado, porEstado.Sum(e => e.Cantidad));
    }

    private static string NombreDe(EstadoEvento estado) => Estados.First(e => e.Estado == estado).Nombre;

    // En el Excel las fechas van en la hora de Bolivia, no en la del servidor.
    private static void Fecha(IXLCell celda, DateTime? utc)
    {
        if (utc is not { } valor) return;
        celda.Value = HoraBolivia.DesdeUtc(valor);
        celda.Style.DateFormat.Format = FormatoFecha;
    }
}
