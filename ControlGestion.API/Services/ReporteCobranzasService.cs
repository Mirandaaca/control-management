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
/// Reporte de cobranzas (3.3, 7.3.12): filtros de estado, promotor, cliente y días de emisión, totales por estado y Excel.
/// Respeta la cartera como la lista.
/// </summary>
public class ReporteCobranzasService(AppDbContext db, IUsuarioActual usuario, TimeProvider reloj)
{
    private const string FormatoFecha = "dd/mm/yyyy";

    // El orden del 8.6.4: primero lo que se debe, después lo pagado y, al final, lo que ya no se cobra.
    private static readonly (EstadoCobranza Estado, string Nombre)[] Estados =
    [
        (EstadoCobranza.Pendiente, "Pendiente"), (EstadoCobranza.Parcial, "Parcial"), (EstadoCobranza.Vencida, "Vencida"),
        (EstadoCobranza.Pagada, "Pagada"), (EstadoCobranza.Incobrable, "Incobrable"), (EstadoCobranza.Anulada, "Anulada")
    ];

    // Una incobrable o una anulada ya no se cobran: se ven en su fila, pero no suman a la fila Total.
    private static readonly EstadoCobranza[] FueraDelTotal = [EstadoCobranza.Incobrable, EstadoCobranza.Anulada];

    private IQueryable<Cobranza> Filtradas(FiltrosCobranzaDTO filtros) => db.Cobranzas.VisiblesPara(usuario).Filtrar(filtros);

    // De la que vence primero a la última (8.6.4).
    private static IQueryable<Cobranza> Ordenadas(IQueryable<Cobranza> consulta) =>
        consulta.OrderBy(c => c.FechaVencimiento).ThenBy(c => c.Id);

    public async Task<ReporteCobranzasDTO> GenerarAsync(FiltrosCobranzaDTO filtros)
    {
        var consulta = Filtradas(filtros);
        var (porEstado, total) = await TotalesAsync(consulta);
        var cantidad = await consulta.CountAsync();
        var items = await Ordenadas(consulta)
            .Skip((filtros.Pagina - 1) * filtros.Tamano)
            .Take(filtros.Tamano)
            .Select(CobranzaConsultas.ComoResumen)
            .ToListAsync();
        var hoy = HoraBolivia.Hoy(reloj);
        var conMora = items.Select(c => c with { DiasDeMora = CobranzaConsultas.DiasDeMora(c.Estado, c.FechaVencimiento, hoy) })
            .ToList();
        return new ReporteCobranzasDTO(porEstado, total, new Pagina<CobranzaResumenDTO>(conMora, cantidad, filtros.Pagina, filtros.Tamano));
    }

    /// <summary>
    /// El reporte por cliente (tarea 30): una fila por cliente con lo que debe, de quien más debe a quien menos, y el detalle
    /// de sus cobranzas. Los mismos filtros, la misma cartera y los mismos totales por estado que el reporte.
    /// </summary>
    public async Task<ReporteCobranzasPorClienteDTO> PorClienteAsync(FiltrosCobranzaDTO filtros)
    {
        var consulta = Filtradas(filtros);
        var (porEstado, total) = await TotalesAsync(consulta);
        var filas = PorCliente(consulta);
        var cantidad = await filas.CountAsync();
        var pagina = await filas.Skip((filtros.Pagina - 1) * filtros.Tamano).Take(filtros.Tamano).ToListAsync();

        var hoy = HoraBolivia.Hoy(reloj);
        var ids = pagina.Select(f => f.ClienteId).ToList();
        var cobranzas = (await Ordenadas(consulta.Where(c => ids.Contains(c.ClienteId))).Select(CobranzaConsultas.ComoResumen).ToListAsync())
            .Select(c => c with { DiasDeMora = CobranzaConsultas.DiasDeMora(c.Estado, c.FechaVencimiento, hoy) })
            .ToLookup(c => c.Cliente.Id);
        var clientes = pagina.Select(f => new DeudaDelClienteDTO(
            new ClienteResumenDTO(f.ClienteId, f.Nombre),
            f.PromotorId == null ? null : new UsuarioResumenDTO(f.PromotorId, f.PromotorNombre ?? string.Empty),
            f.Cantidad, f.Emitido, f.Cobrado, f.Saldo, f.Vencido, DiasDeMora(f, hoy),
            cobranzas[f.ClienteId].ToList())).ToList();
        return new ReporteCobranzasPorClienteDTO(porEstado, total,
            new Pagina<DeudaDelClienteDTO>(clientes, cantidad, filtros.Pagina, filtros.Tamano));
    }

    /// <summary>
    /// Una fila por cliente, sumada en la base. Como la fila Total, lo emitido, lo cobrado y el saldo no suman las
    /// incobrables ni las anuladas. Se proyecta con propiedades, no con un constructor, para ordenar y paginar sobre ellas.
    /// El estado se compara uno por uno, sin Contains (la trampa de EF Core 10 del CLAUDE.md).
    /// </summary>
    private static IQueryable<FilaDelCliente> PorCliente(IQueryable<Cobranza> consulta) => consulta
        .GroupBy(c => new
        {
            c.ClienteId, c.Cliente!.NombreCompleto, c.Cliente.PromotorId, PromotorNombre = c.Cliente.Promotor!.NombreCompleto
        })
        .Select(g => new FilaDelCliente
        {
            ClienteId = g.Key.ClienteId,
            Nombre = g.Key.NombreCompleto,
            PromotorId = g.Key.PromotorId,
            PromotorNombre = g.Key.PromotorNombre,
            Cantidad = g.Count(),
            Emitido = g.Sum(c => c.Estado != EstadoCobranza.Incobrable && c.Estado != EstadoCobranza.Anulada ? c.MontoTotal : 0m),
            Cobrado = g.Sum(c => c.Estado != EstadoCobranza.Incobrable && c.Estado != EstadoCobranza.Anulada ? c.MontoPagado : 0m),
            Saldo = g.Sum(c => c.Estado != EstadoCobranza.Incobrable && c.Estado != EstadoCobranza.Anulada && c.MontoTotal > c.MontoPagado
                ? c.MontoTotal - c.MontoPagado : 0m),
            Vencido = g.Sum(c => c.Estado == EstadoCobranza.Vencida && c.MontoTotal > c.MontoPagado ? c.MontoTotal - c.MontoPagado : 0m),
            VenceLaMasAtrasada = g.Min(c => c.Estado == EstadoCobranza.Vencida ? (DateTime?)c.FechaVencimiento : null),
        })
        .OrderByDescending(f => f.Saldo).ThenBy(f => f.Nombre).ThenBy(f => f.ClienteId);

    // La mora más larga de un cliente: la de su vencida más atrasada.
    private static int? DiasDeMora(FilaDelCliente fila, DateOnly hoy) => fila.VenceLaMasAtrasada is { } vence
        ? CobranzaConsultas.DiasDeMora(EstadoCobranza.Vencida, vence, hoy)
        : null;

    private sealed class FilaDelCliente
    {
        public int ClienteId { get; init; }
        public string Nombre { get; init; } = string.Empty;
        public string? PromotorId { get; init; }
        public string? PromotorNombre { get; init; }
        public int Cantidad { get; init; }
        public decimal Emitido { get; init; }
        public decimal Cobrado { get; init; }
        public decimal Saldo { get; init; }
        public decimal Vencido { get; init; }
        public DateTime? VenceLaMasAtrasada { get; init; }
    }

    /// <summary>
    /// El Excel del reporte: todas las cobranzas de los filtros, sin paginar, y una hoja con los totales. Sin cobranzas no
    /// se exporta (7.3.12, ext. 2.1a).
    /// </summary>
    public async Task<(byte[] Contenido, string NombreArchivo)> ExcelAsync(FiltrosCobranzaDTO filtros)
    {
        var consulta = Filtradas(filtros);
        if (!await consulta.AnyAsync())
            throw new ReglaDeNegocioException("reporte_sin_datos", "No hay cobranzas con esos filtros: no hay nada que exportar.");
        var (porEstado, total) = await TotalesAsync(consulta);
        var filas = await Ordenadas(consulta).Select(CobranzaConsultas.ComoResumen).ToListAsync();
        var hoy = HoraBolivia.Hoy(reloj);

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Cobranzas");
        string[] encabezados = ["Número", "Cliente", "Promotor", "Emitida", "Vence", "Días de mora", "Total (Bs)", "Pagado (Bs)",
            "Saldo (Bs)", "Estado"];
        for (var c = 0; c < encabezados.Length; c++) hoja.Cell(1, c + 1).Value = encabezados[c];
        hoja.Row(1).Style.Font.Bold = true;

        var fila = 2;
        foreach (var c in filas)
        {
            hoja.Cell(fila, 1).Value = c.Numero;
            hoja.Cell(fila, 2).Value = c.Cliente.NombreCompleto;
            hoja.Cell(fila, 3).Value = c.Promotor.NombreCompleto;
            Fecha(hoja.Cell(fila, 4), c.FechaEmision);
            Fecha(hoja.Cell(fila, 5), c.FechaVencimiento);
            hoja.Cell(fila, 6).Value = CobranzaConsultas.DiasDeMora(c.Estado, c.FechaVencimiento, hoy);
            hoja.Cell(fila, 7).Value = c.MontoTotal;
            hoja.Cell(fila, 8).Value = c.MontoPagado;
            hoja.Cell(fila, 9).Value = c.SaldoPendiente;
            hoja.Cell(fila, 10).Value = NombreDe(c.Estado);
            fila++;
        }
        hoja.Range(2, 7, Math.Max(fila - 1, 2), 9).Style.NumberFormat.Format = "#,##0.00";
        hoja.Columns().AdjustToContents();

        // Lo que debe cada cliente (tarea 30), de quien más debe a quien menos, con los mismos filtros.
        var porCliente = libro.Worksheets.Add("Por cliente");
        string[] deCliente = ["Cliente", "Promotor", "Cobranzas", "Emitido (Bs)", "Cobrado (Bs)", "Saldo (Bs)", "Vencido (Bs)", "Días de mora"];
        for (var c = 0; c < deCliente.Length; c++) porCliente.Cell(1, c + 1).Value = deCliente[c];
        porCliente.Row(1).Style.Font.Bold = true;
        var filaDeCliente = 2;
        foreach (var f in await PorCliente(consulta).ToListAsync())
        {
            porCliente.Cell(filaDeCliente, 1).Value = f.Nombre;
            porCliente.Cell(filaDeCliente, 2).Value = f.PromotorNombre ?? "";
            porCliente.Cell(filaDeCliente, 3).Value = f.Cantidad;
            porCliente.Cell(filaDeCliente, 4).Value = f.Emitido;
            porCliente.Cell(filaDeCliente, 5).Value = f.Cobrado;
            porCliente.Cell(filaDeCliente, 6).Value = f.Saldo;
            porCliente.Cell(filaDeCliente, 7).Value = f.Vencido;
            porCliente.Cell(filaDeCliente, 8).Value = DiasDeMora(f, hoy);
            filaDeCliente++;
        }
        porCliente.Range(2, 4, Math.Max(filaDeCliente - 1, 2), 7).Style.NumberFormat.Format = "#,##0.00";
        if (porEstado.Any(e => FueraDelTotal.Contains(e.Estado)))
            porCliente.Cell(filaDeCliente + 1, 1).Value = "Los montos de cada cliente no suman las cobranzas incobrables ni las anuladas.";
        porCliente.Columns(1, 8).AdjustToContents();

        var totales = libro.Worksheets.Add("Totales por estado");
        string[] columnas = ["Estado", "Cobranzas", "Emitido (Bs)", "Cobrado (Bs)", "Saldo (Bs)"];
        for (var c = 0; c < columnas.Length; c++) totales.Cell(1, c + 1).Value = columnas[c];
        totales.Row(1).Style.Font.Bold = true;
        var t = 2;
        foreach (var e in porEstado)
        {
            totales.Cell(t, 1).Value = e.Nombre;
            Cifras(totales, t, e.Cantidad, e.Emitido, e.Cobrado, e.Saldo);
            t++;
        }
        totales.Cell(t, 1).Value = "Total";
        Cifras(totales, t, total.Cantidad, total.Emitido, total.Cobrado, total.Saldo);
        totales.Row(t).Style.Font.Bold = true;
        if (porEstado.Any(e => FueraDelTotal.Contains(e.Estado)))
            totales.Cell(t + 2, 1).Value = "La fila Total no suma las cobranzas incobrables ni las anuladas.";
        totales.Range(2, 3, t, 5).Style.NumberFormat.Format = "#,##0.00";
        totales.Columns(1, 5).AdjustToContents();

        using var salida = new MemoryStream();
        libro.SaveAs(salida);
        return (salida.ToArray(), $"cobranzas_{hoy:yyyyMMdd}.xlsx");
    }

    /// <summary>Los estados que tienen cobranzas, en el orden del 8.6.4, y la fila Total sin las que ya no se cobran.</summary>
    private static async Task<(List<TotalCobranzasPorEstadoDTO> PorEstado, TotalCobranzasDTO Total)> TotalesAsync(
        IQueryable<Cobranza> consulta)
    {
        var agrupadas = await consulta
            .GroupBy(c => c.Estado)
            .Select(g => new
            {
                Estado = g.Key,
                Cantidad = g.Count(),
                Emitido = g.Sum(c => c.MontoTotal),
                Cobrado = g.Sum(c => c.MontoPagado),
                Saldo = g.Sum(c => c.MontoTotal - c.MontoPagado > 0 ? c.MontoTotal - c.MontoPagado : 0m)
            })
            .ToDictionaryAsync(g => g.Estado);

        var porEstado = Estados
            .Where(e => agrupadas.ContainsKey(e.Estado))
            .Select(e => (e.Estado, e.Nombre, Grupo: agrupadas[e.Estado]))
            .Select(e => new TotalCobranzasPorEstadoDTO(e.Estado, e.Nombre, e.Grupo.Cantidad, e.Grupo.Emitido, e.Grupo.Cobrado,
                e.Grupo.Saldo))
            .ToList();
        var vigentes = porEstado.Where(e => !FueraDelTotal.Contains(e.Estado)).ToList();
        var total = new TotalCobranzasDTO(vigentes.Sum(e => e.Cantidad), vigentes.Sum(e => e.Emitido), vigentes.Sum(e => e.Cobrado),
            vigentes.Sum(e => e.Saldo));
        return (porEstado, total);
    }

    private static string NombreDe(EstadoCobranza estado) => Estados.First(e => e.Estado == estado).Nombre;

    private static void Cifras(IXLWorksheet hoja, int fila, int cantidad, decimal emitido, decimal cobrado, decimal saldo)
    {
        hoja.Cell(fila, 2).Value = cantidad;
        hoja.Cell(fila, 3).Value = emitido;
        hoja.Cell(fila, 4).Value = cobrado;
        hoja.Cell(fila, 5).Value = saldo;
    }

    // En el Excel las fechas van en la hora de Bolivia, no en la del servidor.
    private static void Fecha(IXLCell celda, DateTime utc)
    {
        celda.Value = HoraBolivia.DesdeUtc(utc).Date;
        celda.Style.DateFormat.Format = FormatoFecha;
    }
}
