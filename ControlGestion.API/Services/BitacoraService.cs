using ClosedXML.Excel;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>
/// Consultar y exportar la bitácora (8.5). La escribe AuditoriaMiddleware; aquí solo se lee: los registros no se
/// modifican ni se eliminan.
/// </summary>
public class BitacoraService(AppDbContext db, TimeProvider reloj)
{
    private const string FormatoFecha = "dd/mm/yyyy hh:mm:ss";

    public async Task<Pagina<RegistroBitacoraDTO>> ConsultarAsync(FiltrosBitacoraDTO filtros)
    {
        var consulta = Filtrar(filtros);
        var total = await consulta.CountAsync();
        var items = await Ordenados(consulta)
            .Skip((filtros.Pagina - 1) * filtros.Tamano)
            .Take(filtros.Tamano)
            .Select(ComoDTO)
            .ToListAsync();
        return new Pagina<RegistroBitacoraDTO>(items, total, filtros.Pagina, filtros.Tamano);
    }

    public async Task<OpcionesBitacoraDTO> OpcionesAsync() => new(
        await db.Users.OrderBy(u => u.NombreCompleto).Select(u => new UsuarioResumenDTO(u.Id, u.NombreCompleto)).ToListAsync(),
        OperacionesDeBitacora.Modulos);

    /// <summary>Los registros de los filtros, sin paginar. Sin registros no se exporta (7.3.35, ext. 2.1a).</summary>
    public async Task<(byte[] Contenido, string NombreArchivo)> ExcelAsync(FiltrosBitacoraDTO filtros)
    {
        var filas = await Ordenados(Filtrar(filtros)).Select(ComoDTO).ToListAsync();
        if (filas.Count == 0)
            throw new ReglaDeNegocioException("reporte_sin_datos", "No hay registros con esos filtros: no hay nada que exportar.");

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Bitácora");
        string[] encabezados = ["Fecha y hora", "Usuario", "Módulo", "Acción", "Dirección IP"];
        for (var c = 0; c < encabezados.Length; c++) hoja.Cell(1, c + 1).Value = encabezados[c];
        hoja.Row(1).Style.Font.Bold = true;

        var fila = 2;
        foreach (var r in filas)
        {
            hoja.Cell(fila, 1).Value = HoraBolivia.DesdeUtc(r.Fecha);
            hoja.Cell(fila, 1).Style.DateFormat.Format = FormatoFecha;
            hoja.Cell(fila, 2).Value = r.Usuario?.NombreCompleto ?? "—";
            hoja.Cell(fila, 3).Value = r.Modulo;
            hoja.Cell(fila, 4).Value = r.Accion;
            hoja.Cell(fila, 5).Value = r.DireccionIp;
            fila++;
        }
        hoja.Columns().AdjustToContents();

        using var salida = new MemoryStream();
        libro.SaveAs(salida);
        return (salida.ToArray(), $"bitacora_{HoraBolivia.Hoy(reloj):yyyyMMdd}.xlsx");
    }

    private IQueryable<AuditoriaLog> Filtrar(FiltrosBitacoraDTO filtros)
    {
        var consulta = db.AuditoriaLogs.AsQueryable();
        if (!string.IsNullOrEmpty(filtros.UsuarioId)) consulta = consulta.Where(a => a.UsuarioId == filtros.UsuarioId);
        if (!string.IsNullOrEmpty(filtros.Modulo)) consulta = consulta.Where(a => a.Modulo == filtros.Modulo);
        if (filtros.Desde is { } desde)
        {
            var inicio = HoraBolivia.InicioDelDiaUtc(desde);
            consulta = consulta.Where(a => a.Fecha >= inicio);
        }
        if (filtros.Hasta is { } hasta)
        {
            var fin = HoraBolivia.FinDelDiaUtc(hasta);
            consulta = consulta.Where(a => a.Fecha < fin);
        }
        return consulta;
    }

    // Del más reciente al más antiguo (7.3.35, paso 2.2); el id desempata los del mismo instante.
    private static IQueryable<AuditoriaLog> Ordenados(IQueryable<AuditoriaLog> consulta) =>
        consulta.OrderByDescending(a => a.Fecha).ThenByDescending(a => a.Id);

    private static readonly System.Linq.Expressions.Expression<Func<AuditoriaLog, RegistroBitacoraDTO>> ComoDTO =
        a => new RegistroBitacoraDTO(
            a.Id,
            a.Fecha,
            a.Usuario == null ? null : new UsuarioResumenDTO(a.Usuario.Id, a.Usuario.NombreCompleto),
            a.Modulo,
            a.Accion,
            a.DireccionIp);
}
