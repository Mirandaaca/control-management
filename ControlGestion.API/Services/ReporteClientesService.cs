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
/// Clientes nuevos y recurrentes (6.3, 7.3.24), con la cartera del usuario. Nuevo es el registrado en el período;
/// recurrente, el registrado antes con al menos una HP confirmada en el período. Un nuevo que compró sale solo como nuevo.
/// </summary>
public class ReporteClientesService(AppDbContext db, IUsuarioActual usuario, TimeProvider reloj, GeneradorDePdf pdf)
{
    private const string FormatoFecha = "dd/mm/yyyy hh:mm";

    private sealed record Periodo(DateTime Inicio, DateTime Fin);

    // Las validaciones del DTO ya pidieron las dos fechas.
    private static Periodo DelFiltro(FiltrosReporteClientesDTO filtros) =>
        new(HoraBolivia.InicioDelDiaUtc(filtros.Desde!.Value), HoraBolivia.FinDelDiaUtc(filtros.Hasta!.Value));

    private IQueryable<ClienteNuevoDTO> Nuevos(Periodo p)
    {
        var (inicio, fin) = (p.Inicio, p.Fin);
        return db.ClientesVisiblesPara(usuario, reloj)
            .Where(c => c.FechaCreacion >= inicio && c.FechaCreacion < fin)
            .OrderByDescending(c => c.FechaCreacion).ThenByDescending(c => c.Id)
            .Select(c => new ClienteNuevoDTO(c.Id, c.Codigo, c.NombreCompleto, new ZonaResumenDTO(c.Zona!.Id, c.Zona.Nombre),
                c.Promotor == null ? null : new UsuarioResumenDTO(c.Promotor.Id, c.Promotor.NombreCompleto), c.FechaCreacion));
    }

    private IQueryable<ClienteRecurrenteDTO> Recurrentes(Periodo p)
    {
        var (inicio, fin) = (p.Inicio, p.Fin);
        return db.ClientesVisiblesPara(usuario, reloj)
            .Where(c => c.FechaCreacion < p.Inicio)
            .Select(c => new
            {
                Cliente = c,
                Compras = db.HojasPedido.Count(h => h.ClienteId == c.Id && h.EstadoHP == EstadoHP.Confirmada
                    && h.FechaRegistro >= inicio && h.FechaRegistro < fin)
            })
            .Where(x => x.Compras > 0)
            .OrderByDescending(x => x.Compras).ThenBy(x => x.Cliente.NombreCompleto).ThenBy(x => x.Cliente.Id)
            .Select(x => new ClienteRecurrenteDTO(x.Cliente.Id, x.Cliente.Codigo, x.Cliente.NombreCompleto,
                new ZonaResumenDTO(x.Cliente.Zona!.Id, x.Cliente.Zona.Nombre),
                x.Cliente.Promotor == null ? null : new UsuarioResumenDTO(x.Cliente.Promotor.Id, x.Cliente.Promotor.NombreCompleto),
                x.Compras));
    }

    public async Task<ReporteClientesDTO> GenerarAsync(FiltrosReporteClientesDTO filtros)
    {
        var periodo = DelFiltro(filtros);
        var nuevos = Nuevos(periodo);
        var recurrentes = Recurrentes(periodo);
        var (cantidadNuevos, cantidadRecurrentes) = (await nuevos.CountAsync(), await recurrentes.CountAsync());

        var paginaNuevos = await nuevos.Skip((filtros.PaginaNuevos - 1) * filtros.Tamano).Take(filtros.Tamano).ToListAsync();
        var paginaRecurrentes = await recurrentes.Skip((filtros.PaginaRecurrentes - 1) * filtros.Tamano).Take(filtros.Tamano).ToListAsync();
        return new ReporteClientesDTO(cantidadNuevos, cantidadRecurrentes,
            new Pagina<ClienteNuevoDTO>(paginaNuevos, cantidadNuevos, filtros.PaginaNuevos, filtros.Tamano),
            new Pagina<ClienteRecurrenteDTO>(paginaRecurrentes, cantidadRecurrentes, filtros.PaginaRecurrentes, filtros.Tamano));
    }

    /// <summary>
    /// Lo que se exporta, a Excel o a PDF: las dos listas completas. Sin clientes en el período no se exporta (7.3.24,
    /// ext. 2.3a).
    /// </summary>
    private async Task<(List<ClienteNuevoDTO> Nuevos, List<ClienteRecurrenteDTO> Recurrentes)> ParaExportarAsync(
        FiltrosReporteClientesDTO filtros)
    {
        var periodo = DelFiltro(filtros);
        var nuevos = await Nuevos(periodo).ToListAsync();
        var recurrentes = await Recurrentes(periodo).ToListAsync();
        if (nuevos.Count == 0 && recurrentes.Count == 0)
            throw new ReglaDeNegocioException("reporte_sin_datos",
                "No hay clientes nuevos ni recurrentes en el período: no hay nada que exportar.");
        return (nuevos, recurrentes);
    }

    /// <summary>El Excel: los nuevos, los recurrentes y sus cantidades, cada uno en su hoja.</summary>
    public async Task<(byte[] Contenido, string NombreArchivo)> ExcelAsync(FiltrosReporteClientesDTO filtros)
    {
        var (nuevos, recurrentes) = await ParaExportarAsync(filtros);

        using var libro = new XLWorkbook();
        var hojaNuevos = libro.Worksheets.Add("Nuevos");
        Encabezados(hojaNuevos, "Código", "Cliente", "Zona", "Promotor", "Registrado");
        var fila = 2;
        foreach (var c in nuevos)
        {
            Datos(hojaNuevos, fila, c.Codigo, c.NombreCompleto, c.Zona.Nombre, c.Promotor?.NombreCompleto);
            hojaNuevos.Cell(fila, 5).Value = HoraBolivia.DesdeUtc(c.FechaRegistro);
            hojaNuevos.Cell(fila, 5).Style.DateFormat.Format = FormatoFecha;
            fila++;
        }
        hojaNuevos.Columns().AdjustToContents();

        var hojaRecurrentes = libro.Worksheets.Add("Recurrentes");
        Encabezados(hojaRecurrentes, "Código", "Cliente", "Zona", "Promotor", "Compras");
        fila = 2;
        foreach (var c in recurrentes)
        {
            Datos(hojaRecurrentes, fila, c.Codigo, c.NombreCompleto, c.Zona.Nombre, c.Promotor?.NombreCompleto);
            hojaRecurrentes.Cell(fila, 5).Value = c.Compras;
            fila++;
        }
        hojaRecurrentes.Columns().AdjustToContents();

        var totales = libro.Worksheets.Add("Totales");
        Encabezados(totales, "Período", "Clientes nuevos", "Clientes recurrentes");
        totales.Cell(2, 1).Value = $"{filtros.Desde:dd/MM/yyyy} al {filtros.Hasta:dd/MM/yyyy}";
        totales.Cell(2, 2).Value = nuevos.Count;
        totales.Cell(2, 3).Value = recurrentes.Count;
        totales.Columns().AdjustToContents();

        using var salida = new MemoryStream();
        libro.SaveAs(salida);
        return (salida.ToArray(), $"clientes_{HoraBolivia.Hoy(reloj):yyyyMMdd}.xlsx");
    }

    /// <summary>El PDF (tarea 33): las cantidades de cada grupo y las dos listas, como el Excel.</summary>
    public async Task<(byte[] Contenido, string NombreArchivo)> PdfAsync(FiltrosReporteClientesDTO filtros)
    {
        var (nuevos, recurrentes) = await ParaExportarAsync(filtros);
        var columnas = new[] { new ColumnaPdf("Código", 1.1f), new ColumnaPdf("Cliente", 2.4f), new ColumnaPdf("Zona", 1.6f),
            new ColumnaPdf("Promotor", 2f) };

        var reporte = new ReportePdf("Reporte de clientes nuevos y recurrentes",
            [new FiltroPdf("Período", TextosDelPdf.Periodo(filtros.Desde, filtros.Hasta)!)],
            [
                new SeccionPdf("Cantidades", [new ColumnaPdf("Grupo", 3), new ColumnaPdf("Clientes", 1, Derecha: true)],
                    [
                        TextosDelPdf.Fila("Clientes nuevos (registrados en el período)", TextosDelPdf.Cantidad(nuevos.Count)),
                        TextosDelPdf.Fila("Clientes recurrentes (registrados antes, con compras en el período)",
                            TextosDelPdf.Cantidad(recurrentes.Count))
                    ],
                    AnchoMaximo: 440),
                new SeccionPdf("Clientes nuevos", [.. columnas, new ColumnaPdf("Registrado", 1.4f)],
                    nuevos.Select(c => TextosDelPdf.Fila(c.Codigo, c.NombreCompleto, c.Zona.Nombre,
                        TextosDelPdf.Texto(c.Promotor?.NombreCompleto), TextosDelPdf.FechaHora(c.FechaRegistro))).ToList(),
                    SiVacia: "Ningún cliente se registró en el período."),
                new SeccionPdf("Clientes recurrentes", [.. columnas, new ColumnaPdf("Compras", 1f, Derecha: true)],
                    recurrentes.Select(c => TextosDelPdf.Fila(c.Codigo, c.NombreCompleto, c.Zona.Nombre,
                        TextosDelPdf.Texto(c.Promotor?.NombreCompleto), TextosDelPdf.Cantidad(c.Compras))).ToList(),
                    SiVacia: "Ningún cliente de antes compró en el período.")
            ]);
        return (pdf.Generar(reporte), $"clientes_{HoraBolivia.Hoy(reloj):yyyyMMdd}.pdf");
    }

    private static void Encabezados(IXLWorksheet hoja, params string[] encabezados)
    {
        for (var c = 0; c < encabezados.Length; c++) hoja.Cell(1, c + 1).Value = encabezados[c];
        hoja.Row(1).Style.Font.Bold = true;
    }

    private static void Datos(IXLWorksheet hoja, int fila, string codigo, string nombre, string zona, string? promotor)
    {
        hoja.Cell(fila, 1).Value = codigo;
        hoja.Cell(fila, 2).Value = nombre;
        hoja.Cell(fila, 3).Value = zona;
        hoja.Cell(fila, 4).Value = promotor;
    }
}
