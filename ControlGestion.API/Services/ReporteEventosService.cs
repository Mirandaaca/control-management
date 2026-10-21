using ClosedXML.Excel;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>
/// Reporte de incidencias (4.4): filtros de tipo, estado, promotor, zona y fechas, totales por tipo y por estado, y
/// Excel. Respeta la cartera como la lista.
/// </summary>
public class ReporteEventosService(AppDbContext db, IUsuarioActual usuario, TimeProvider reloj)
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

    /// <summary>
    /// El Excel del reporte: todos los eventos de los filtros, sin paginar, y una hoja con los totales. Sin eventos no se
    /// exporta (7.3.16).
    /// </summary>
    public async Task<(byte[] Contenido, string NombreArchivo)> ExcelAsync(FiltrosEventoDTO filtros)
    {
        var consulta = Filtrados(filtros);
        var (porTipo, porEstado, cantidad) = await TotalesAsync(consulta);
        if (cantidad == 0)
            throw new ReglaDeNegocioException("reporte_sin_datos", "No hay eventos con esos filtros: no hay nada que exportar.");

        var filas = await consulta
            .OrderBy(e => e.FechaEvento).ThenBy(e => e.Id)
            .Select(e => new
            {
                e.Numero,
                e.FechaEvento,
                Tipo = e.TipoEvento!.Nombre,
                e.TipoEvento.NivelPrioridad,
                e.EstadoEvento,
                Cliente = e.Cliente == null ? null : e.Cliente.NombreCompleto,
                Pedido = e.Pedido == null ? null : e.Pedido.Numero,
                Zona = e.Cliente == null ? null : e.Cliente.Zona!.Nombre,
                Registro = e.Promotor!.NombreCompleto,
                e.Descripcion,
                Fotos = e.Evidencias.Count,
                e.Solucion,
                ResueltoPor = e.ResueltoPor == null ? null : e.ResueltoPor.NombreCompleto,
                e.FechaResolucion,
                e.Latitud,
                e.Longitud
            })
            .ToListAsync();

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

    /// <summary>Cuántos eventos hay de cada tipo y en cada estado. Salen todos los tipos y los dos estados, también en cero.</summary>
    private async Task<(List<TotalPorTipoDTO> PorTipo, List<TotalPorEstadoEventoDTO> PorEstado, int Cantidad)> TotalesAsync(
        IQueryable<Evento> consulta)
    {
        var porTipoId = await consulta.GroupBy(e => e.TipoEventoId)
            .Select(g => new { TipoId = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.TipoId, g => g.Cantidad);
        var porEstadoId = await consulta.GroupBy(e => e.EstadoEvento)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.Estado, g => g.Cantidad);
        var tipos = await db.TiposEvento.OrderBy(t => t.Id).Select(t => new { t.Id, t.Nombre }).ToListAsync();

        var porTipo = tipos.Select(t => new TotalPorTipoDTO(t.Id, t.Nombre, porTipoId.GetValueOrDefault(t.Id))).ToList();
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
