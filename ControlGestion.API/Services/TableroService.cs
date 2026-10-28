using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>
/// El tablero de Inicio: los indicadores y las ventas del mes en curso en Bolivia, con la cartera del usuario. Un
/// promotor ve los de sus clientes, no los de toda la empresa. El stock es de la empresa.
/// </summary>
public class TableroService(AppDbContext db, IUsuarioActual usuario, TimeProvider reloj)
{
    public async Task<TableroDTO> ResumenAsync()
    {
        var hoy = HoraBolivia.Hoy(reloj);
        var inicioDelMes = HoraBolivia.InicioDelDiaUtc(new DateOnly(hoy.Year, hoy.Month, 1));
        var delMes = db.HojasPedido.VisiblesPara(usuario)
            .Where(h => h.EstadoHP == EstadoHP.Confirmada && h.FechaRegistro >= inicioDelMes);

        var ventas = await delMes.Select(h => new { h.FechaRegistro, h.Total }).ToListAsync();
        var porPromotor = await delMes
            .GroupBy(h => new { h.PromotorId, h.Promotor!.NombreCompleto })
            .Select(g => new VentasDePromotorDTO(g.Key.PromotorId, g.Key.NombreCompleto, g.Count(), g.Sum(h => h.Total)))
            .ToListAsync();
        var porProducto = (await delMes
                .SelectMany(h => h.Detalles)
                .GroupBy(d => d.Producto!.Descripcion)
                .Select(g => new { Producto = g.Key, Cantidad = g.Sum(d => d.Cantidad), Total = g.Sum(d => d.Subtotal) })
                .OrderByDescending(p => p.Total)
                .Take(10)
                .ToListAsync())
            .Select(p => new VentasDeProductoDTO(p.Producto, p.Cantidad, p.Total))
            .ToList();
        // El día de cada venta es el de Bolivia: una HP de las 21:00 no cae en el día siguiente.
        var porDia = ventas
            .GroupBy(v => HoraBolivia.DiaDe(v.FechaRegistro))
            .OrderBy(g => g.Key)
            .Select(g => new VentasDelDiaDTO(g.Key.ToString("yyyy-MM-dd"), g.Count(), g.Sum(v => v.Total)))
            .ToList();

        return new TableroDTO(
            await db.Clientes.VisiblesPara(usuario).CountAsync(c => c.EstadoClienteId != EstadosDeCliente.Inactivo),
            ventas.Count,
            ventas.Sum(v => v.Total),
            await db.Cobranzas.VisiblesPara(usuario).Where(c => CobranzaConsultas.Abiertas.Contains(c.Estado))
                .SumAsync(c => c.MontoTotal - c.MontoPagado),
            await db.Eventos.VisiblesPara(usuario).CountAsync(e => e.EstadoEvento == EstadoEvento.Pendiente),
            await db.Productos.Where(p => p.Activo).BajoElMinimo().CountAsync(),
            porPromotor.OrderByDescending(p => p.Total).ToList(),
            porProducto,
            porDia);
    }

    // Los permisos que abren cada bloque del análisis: los del reporte o la pantalla con ese dato, como en el panel.
    private static readonly string[] DeVentas =
        [Permisos.Reportes.VentasPorZona, Permisos.Reportes.VentasPorPromotor, Permisos.Ventas.ConsultarHp, Permisos.Ventas.AnularHp];
    private static readonly string[] DePagos = [Permisos.Cobranzas.ConsultarPagos, Permisos.Cobranzas.GenerarReporte];
    private static readonly string[] DeDeudas = [Permisos.Cobranzas.ConsultarDeudas, Permisos.Cobranzas.GenerarReporte];
    private static readonly string[] DeClientes = [Permisos.Reportes.ClientesNuevosRecurrentes, Permisos.Ventas.RegistrarCliente,
        Permisos.Ventas.EditarCliente, Permisos.Ventas.AsignarEstadoCliente, Permisos.Ventas.VerUbicacionCliente];
    private static readonly string[] DeRutas = [Permisos.Territorial.PlanificarRuta, Permisos.Territorial.SeguirRuta];

    private const int MesesDeTendencia = 6;

    /// <summary>
    /// El análisis de Inicio (tarea 24, parte B): la variación contra el mes anterior, la tendencia de las ventas, la
    /// antigüedad de lo que se debe, los mejores clientes y las visitas por promotor.
    /// </summary>
    public async Task<AnalisisDelTableroDTO> AnalisisAsync()
    {
        bool Puede(string[] permisos) => permisos.Any(usuario.Tiene);
        var hoy = HoraBolivia.Hoy(reloj);
        var primero = new DateOnly(hoy.Year, hoy.Month, 1);
        var primeroAnterior = primero.AddMonths(-1);
        // Del 1 al mismo día del mes anterior; si ese mes es más corto (hoy 31, septiembre tiene 30), hasta su último día.
        var hastaAnterior = primeroAnterior.AddDays(
            Math.Min(hoy.Day, DateTime.DaysInMonth(primeroAnterior.Year, primeroAnterior.Month)) - 1);
        var (desde, hasta) = (HoraBolivia.InicioDelDiaUtc(primero), HoraBolivia.FinDelDiaUtc(hoy));
        var (desdeAnterior, hastaAnteriorUtc) = (HoraBolivia.InicioDelDiaUtc(primeroAnterior), HoraBolivia.FinDelDiaUtc(hastaAnterior));

        var confirmadas = db.HojasPedido.VisiblesPara(usuario).Where(h => h.EstadoHP == EstadoHP.Confirmada);
        VariacionDTO? ventas = null;
        List<VentasDelMesDTO>? tendencia = null;
        List<VentasDeClienteDTO>? mejores = null;
        if (Puede(DeVentas))
        {
            ventas = new VariacionDTO(
                await confirmadas.Where(h => h.FechaRegistro >= desde && h.FechaRegistro < hasta).SumAsync(h => h.Total),
                await confirmadas.Where(h => h.FechaRegistro >= desdeAnterior && h.FechaRegistro < hastaAnteriorUtc)
                    .SumAsync(h => h.Total));

            // Agrupadas por mes de Bolivia en la base: 6 meses de HP no se traen a memoria.
            var primeroDeLaTendencia = primero.AddMonths(1 - MesesDeTendencia);
            var desdeLaTendencia = HoraBolivia.InicioDelDiaUtc(primeroDeLaTendencia);
            var porMes = await confirmadas
                .Where(h => h.FechaRegistro >= desdeLaTendencia && h.FechaRegistro < hasta)
                .Select(h => new { Fecha = h.FechaRegistro.AddHours(HoraBolivia.DesfaseEnHoras), h.TipoVenta, h.Total })
                .GroupBy(h => new { h.Fecha.Year, h.Fecha.Month, h.TipoVenta })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.TipoVenta, Total = g.Sum(h => h.Total) })
                .ToListAsync();
            decimal Del(DateOnly mes, TipoVenta tipo) =>
                porMes.Where(m => m.Year == mes.Year && m.Month == mes.Month && m.TipoVenta == tipo).Sum(m => m.Total);
            tendencia = Enumerable.Range(0, MesesDeTendencia)
                .Select(i => primeroDeLaTendencia.AddMonths(i))
                .Select(mes => new VentasDelMesDTO($"{mes:yyyy-MM}", Del(mes, TipoVenta.Contado), Del(mes, TipoVenta.Credito)))
                .ToList();

            mejores = await confirmadas
                .Where(h => h.FechaRegistro >= desde && h.FechaRegistro < hasta)
                .GroupBy(h => new { h.ClienteId, h.Cliente!.NombreCompleto })
                .OrderByDescending(g => g.Sum(h => h.Total))
                .Take(5)
                .Select(g => new VentasDeClienteDTO(g.Key.ClienteId, g.Key.NombreCompleto, g.Count(), g.Sum(h => h.Total)))
                .ToListAsync();
        }

        VariacionDTO? cobrado = null;
        if (Puede(DePagos))
        {
            var pagos = db.Cobranzas.VisiblesPara(usuario).SelectMany(c => c.Pagos).Where(p => !p.Anulado);
            cobrado = new VariacionDTO(
                await pagos.Where(p => p.FechaPago >= desde && p.FechaPago < hasta).SumAsync(p => p.Monto),
                await pagos.Where(p => p.FechaPago >= desdeAnterior && p.FechaPago < hastaAnteriorUtc).SumAsync(p => p.Monto));
        }

        VariacionDTO? clientesNuevos = null;
        if (Puede(DeClientes))
        {
            var clientes = db.Clientes.VisiblesPara(usuario);
            clientesNuevos = new VariacionDTO(
                await clientes.CountAsync(c => c.FechaCreacion >= desde && c.FechaCreacion < hasta),
                await clientes.CountAsync(c => c.FechaCreacion >= desdeAnterior && c.FechaCreacion < hastaAnteriorUtc));
        }

        List<AntiguedadDeSaldosDTO>? antiguedad = null;
        if (Puede(DeDeudas))
        {
            // Los días de mora, como en Cobranzas: solo una Vencida tiene mora; una Pendiente o Parcial está al día.
            var abiertas = (await db.Cobranzas.VisiblesPara(usuario)
                    .Where(c => CobranzaConsultas.Abiertas.Contains(c.Estado))
                    .Select(c => new { c.Estado, c.FechaVencimiento, Saldo = c.MontoTotal - c.MontoPagado })
                    .ToListAsync())
                .Select(c => (Tramo: TramoDeMora(CobranzaConsultas.DiasDeMora(c.Estado, c.FechaVencimiento, hoy)), c.Saldo))
                .ToList();
            antiguedad = TramosDeMora
                .Select(t => new AntiguedadDeSaldosDTO(t, abiertas.Count(c => c.Tramo == t),
                    abiertas.Where(c => c.Tramo == t).Sum(c => c.Saldo)))
                .ToList();
        }

        List<VisitasDePromotorDTO>? visitas = null;
        if (Puede(DeRutas))
        {
            visitas = (await db.Rutas.VisiblesPara(usuario)
                    .Where(r => r.FechaPlanificada >= primero && r.FechaPlanificada <= hoy)
                    .SelectMany(r => r.Detalles, (r, d) => new { r.PromotorId, r.Promotor!.NombreCompleto, d.EstadoVisita })
                    .GroupBy(v => new { v.PromotorId, v.NombreCompleto })
                    .Select(g => new VisitasDePromotorDTO(g.Key.PromotorId, g.Key.NombreCompleto, g.Count(),
                        g.Count(v => v.EstadoVisita == EstadoVisita.Visitado), g.Count(v => v.EstadoVisita == EstadoVisita.Ausente)))
                    .ToListAsync())
                .OrderBy(v => v.Promotor)
                .ToList();
        }

        return new AnalisisDelTableroDTO(hoy, hastaAnterior, ventas, cobrado, clientesNuevos, tendencia, antiguedad, mejores, visitas);
    }

    private static readonly string[] TramosDeMora = ["al-dia", "1-30", "31-60", "61-90", "mas-de-90"];

    private static string TramoDeMora(int? dias) => dias switch
    {
        null => "al-dia",
        <= 30 => "1-30",
        <= 60 => "31-60",
        <= 90 => "61-90",
        _ => "mas-de-90",
    };
}
