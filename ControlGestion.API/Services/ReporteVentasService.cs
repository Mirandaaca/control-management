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
/// Las ventas por zona (6.2, 7.3.23) y por promotor (6.1, 7.3.22): solo las HP confirmadas, por el día de su registro en
/// Bolivia, con la cartera del usuario. Desde la tarea 33 se exportan a Excel y a PDF, como los demás reportes (una
/// extensión nueva de la 7.3.22 y la 7.3.23): sin ventas, no se exporta.
/// </summary>
public class ReporteVentasService(AppDbContext db, IUsuarioActual usuario, TimeProvider reloj)
{
    private const string FormatoMonto = "#,##0.00";
    /// <summary>Las HP confirmadas del período (una anulada ya no es una venta). Sin fechas, todas.</summary>
    private IQueryable<HojaPedido> Confirmadas(FiltrosPeriodoDTO filtros)
    {
        var consulta = db.HojasPedido.VisiblesPara(usuario).Where(h => h.EstadoHP == EstadoHP.Confirmada);
        if (filtros.Desde is { } desde)
        {
            var inicio = HoraBolivia.InicioDelDiaUtc(desde);
            consulta = consulta.Where(h => h.FechaRegistro >= inicio);
        }
        if (filtros.Hasta is { } hasta)
        {
            var fin = HoraBolivia.FinDelDiaUtc(hasta);
            consulta = consulta.Where(h => h.FechaRegistro < fin);
        }
        return consulta;
    }

    public async Task<ReporteVentasPorZonaDTO> PorZonaAsync(FiltrosPeriodoDTO filtros)
    {
        var porZona = await Confirmadas(filtros)
            .GroupBy(h => h.ZonaId)
            .Select(g => new
            {
                ZonaId = g.Key,
                Cantidad = g.Count(),
                Total = g.Sum(h => h.Total),
                Contado = g.Sum(h => h.TipoVenta == TipoVenta.Contado ? h.Total : 0m),
                Credito = g.Sum(h => h.TipoVenta == TipoVenta.Credito ? h.Total : 0m)
            })
            .ToDictionaryAsync(g => g.ZonaId);

        // Las zonas sin ventas salen en cero (ext. 2.3a). Sin cartera completa, las zonas del usuario y las de sus ventas
        // (una HP conserva su zona aunque el cliente se mude).
        var (usuarioId, completa) = (usuario.Id, usuario.VeCarteraCompleta);
        var ventasEn = porZona.Keys.ToList();
        var zonas = await db.Zonas
            .Where(z => completa || z.PromotorId == usuarioId || ventasEn.Contains(z.Id))
            .Select(z => new { z.Id, z.Nombre })
            .ToListAsync();

        var filas = zonas
            .Select(z => porZona.TryGetValue(z.Id, out var v)
                ? new VentasDeZonaDTO(z.Id, z.Nombre, v.Cantidad, v.Total, v.Contado, v.Credito)
                : new VentasDeZonaDTO(z.Id, z.Nombre, 0, 0m, 0m, 0m))
            .OrderByDescending(z => z.Total).ThenBy(z => z.Zona)
            .ToList();
        var total = new TotalVentasDTO(filas.Sum(z => z.Cantidad), filas.Sum(z => z.Total), filas.Sum(z => z.Contado),
            filas.Sum(z => z.Credito));
        return new ReporteVentasPorZonaDTO(filas, total);
    }

    public async Task<ReporteVentasPorPromotorDTO> PorPromotorAsync(FiltrosVentasPorPromotorDTO filtros)
    {
        var consulta = Confirmadas(filtros);
        if (!string.IsNullOrEmpty(filtros.PromotorId)) consulta = consulta.Where(h => h.PromotorId == filtros.PromotorId);

        var promotores = await consulta
            .GroupBy(h => new { h.PromotorId, h.Promotor!.NombreCompleto })
            .Select(g => new VentasDePromotorDTO(g.Key.PromotorId, g.Key.NombreCompleto, g.Count(), g.Sum(h => h.Total)))
            .ToListAsync();
        var ordenados = promotores.OrderByDescending(p => p.Total).ThenBy(p => p.Promotor).ToList();
        return new ReporteVentasPorPromotorDTO(ordenados, ordenados.Sum(p => p.Cantidad), ordenados.Sum(p => p.Total));
    }

    /// <summary>Las zonas del reporte, también las que no vendieron, con la fila Total y el período.</summary>
    public async Task<(byte[] Contenido, string NombreArchivo)> ExcelPorZonaAsync(FiltrosPeriodoDTO filtros)
    {
        var reporte = await PorZonaAsync(filtros);
        if (reporte.Total.Cantidad == 0) throw SinVentas("No hay ventas confirmadas en el período");

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Ventas por zona");
        Encabezados(hoja, "Zona", "HP", "Total", "Contado", "Crédito");
        var fila = 2;
        foreach (var z in reporte.Zonas)
            Fila(hoja, fila++, z.Zona, z.Cantidad, z.Total, z.Contado, z.Credito);
        Fila(hoja, fila, "Total", reporte.Total.Cantidad, reporte.Total.Total, reporte.Total.Contado, reporte.Total.Credito);
        hoja.Row(fila).Style.Font.Bold = true;
        Pie(hoja, fila + 2, ("Período", TextoDelPeriodo(filtros)));
        hoja.Columns().AdjustToContents();
        return (Guardar(libro), $"ventas_por_zona_{HoraBolivia.Hoy(reloj):yyyyMMdd}.xlsx");
    }

    /// <summary>Cada promotor con sus HP y su total, la fila Total, el período y el promotor elegido.</summary>
    public async Task<(byte[] Contenido, string NombreArchivo)> ExcelPorPromotorAsync(FiltrosVentasPorPromotorDTO filtros)
    {
        var reporte = await PorPromotorAsync(filtros);
        if (reporte.Cantidad == 0) throw SinVentas("Ninguna hoja de pedido confirmada coincide con los filtros");

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Ventas por promotor");
        Encabezados(hoja, "Promotor", "HP", "Total vendido");
        var fila = 2;
        foreach (var p in reporte.Promotores)
            Fila(hoja, fila++, p.Promotor, p.Cantidad, p.Total);
        Fila(hoja, fila, "Total", reporte.Cantidad, reporte.Total);
        hoja.Row(fila).Style.Font.Bold = true;
        Pie(hoja, fila + 2, ("Período", TextoDelPeriodo(filtros)), ("Promotor", await NombreDelPromotorAsync(filtros.PromotorId)));
        hoja.Columns().AdjustToContents();
        return (Guardar(libro), $"ventas_por_promotor_{HoraBolivia.Hoy(reloj):yyyyMMdd}.xlsx");
    }

    /// <summary>"01/09/2026 al 29/10/2026", "Desde el …", "Hasta el …" o, sin fechas, todas las ventas (ext. 2).</summary>
    internal static string TextoDelPeriodo(FiltrosPeriodoDTO filtros) => (filtros.Desde, filtros.Hasta) switch
    {
        ({ } desde, { } hasta) => $"{FormatoBolivia.Fecha(desde)} al {FormatoBolivia.Fecha(hasta)}",
        ({ } desde, null) => $"Desde el {FormatoBolivia.Fecha(desde)}",
        (null, { } hasta) => $"Hasta el {FormatoBolivia.Fecha(hasta)}",
        _ => "Todas las ventas"
    };

    /// <summary>El nombre del promotor del filtro, o "Todos" si no se eligió uno.</summary>
    private async Task<string> NombreDelPromotorAsync(string? promotorId) => string.IsNullOrEmpty(promotorId)
        ? "Todos"
        : await db.Users.Where(u => u.Id == promotorId).Select(u => u.NombreCompleto).FirstOrDefaultAsync() ?? "—";

    private static ReglaDeNegocioException SinVentas(string motivo) =>
        new("reporte_sin_datos", $"{motivo}: no hay nada que exportar.");

    private static void Encabezados(IXLWorksheet hoja, params string[] encabezados)
    {
        for (var c = 0; c < encabezados.Length; c++) hoja.Cell(1, c + 1).Value = encabezados[c];
        hoja.Row(1).Style.Font.Bold = true;
    }

    // El nombre, la cantidad de HP y los montos, con formato de dinero.
    private static void Fila(IXLWorksheet hoja, int fila, string nombre, int cantidad, params decimal[] montos)
    {
        hoja.Cell(fila, 1).Value = nombre;
        hoja.Cell(fila, 2).Value = cantidad;
        for (var i = 0; i < montos.Length; i++)
        {
            hoja.Cell(fila, 3 + i).Value = montos[i];
            hoja.Cell(fila, 3 + i).Style.NumberFormat.Format = FormatoMonto;
        }
    }

    private static void Pie(IXLWorksheet hoja, int fila, params (string Etiqueta, string Valor)[] datos)
    {
        foreach (var (etiqueta, valor) in datos)
        {
            hoja.Cell(fila, 1).Value = etiqueta;
            hoja.Cell(fila, 1).Style.Font.Bold = true;
            hoja.Cell(fila++, 2).Value = valor;
        }
    }

    private static byte[] Guardar(XLWorkbook libro)
    {
        using var salida = new MemoryStream();
        libro.SaveAs(salida);
        return salida.ToArray();
    }
}
