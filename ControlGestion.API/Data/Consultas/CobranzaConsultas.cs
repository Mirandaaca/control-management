using System.Linq.Expressions;
using ControlGestion.API.Configuracion;
using ControlGestion.API.DTOs;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;

namespace ControlGestion.API.Data.Consultas;

public static class CobranzaConsultas
{
    /// <summary>Sin seguridad.ver-cartera-completa, el usuario solo ve las cobranzas de los clientes que tiene asignados.</summary>
    public static IQueryable<Cobranza> VisiblesPara(this IQueryable<Cobranza> cobranzas, IUsuarioActual usuario)
    {
        if (usuario.VeCarteraCompleta) return cobranzas;
        var usuarioId = usuario.Id;
        return cobranzas.Where(c => c.Cliente!.PromotorId == usuarioId);
    }

    /// <summary>Busca en el número de la cobranza y en el nombre y el código del cliente.</summary>
    public static IQueryable<Cobranza> Buscar(this IQueryable<Cobranza> cobranzas, string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return cobranzas;
        var t = texto.Trim();
        return cobranzas.Where(c => c.Numero.Contains(t) || c.Cliente!.NombreCompleto.Contains(t) || c.Cliente.Codigo.Contains(t));
    }

    /// <summary>Las que todavía se deben: reciben pagos y cuentan en el saldo del cliente.</summary>
    public static readonly EstadoCobranza[] Abiertas = [EstadoCobranza.Pendiente, EstadoCobranza.Parcial, EstadoCobranza.Vencida];

    /// <summary>Los filtros de la lista (3.2) y del reporte (3.3). Las fechas son de emisión, por día de Bolivia.</summary>
    public static IQueryable<Cobranza> Filtrar(this IQueryable<Cobranza> cobranzas, FiltrosCobranzaDTO filtros)
    {
        var consulta = cobranzas.Buscar(filtros.Buscar);

        if (filtros.ClienteId.HasValue) consulta = consulta.Where(c => c.ClienteId == filtros.ClienteId);
        if (filtros.Estado.HasValue) consulta = consulta.Where(c => c.Estado == filtros.Estado);
        if (filtros.PorCobrar) consulta = consulta.Where(c => Abiertas.Contains(c.Estado));
        if (!string.IsNullOrEmpty(filtros.PromotorId)) consulta = consulta.Where(c => c.PromotorId == filtros.PromotorId);
        if (filtros.Desde is { } desde)
        {
            var inicio = HoraBolivia.InicioDelDiaUtc(desde);
            consulta = consulta.Where(c => c.FechaEmision >= inicio);
        }
        if (filtros.Hasta is { } hasta)
        {
            var fin = HoraBolivia.FinDelDiaUtc(hasta);
            consulta = consulta.Where(c => c.FechaEmision < fin);
        }
        return consulta;
    }

    /// <summary>
    /// Días de calendario de Bolivia desde el día del vencimiento, como "diasDesdeHoy" del panel: una deuda que venció
    /// ayer a las 23:30 lleva 1 día de mora. Solo una Vencida tiene mora.
    /// </summary>
    public static int? DiasDeMora(EstadoCobranza estado, DateTime vencimientoUtc, DateOnly hoy) =>
        estado == EstadoCobranza.Vencida ? hoy.DayNumber - HoraBolivia.DiaDe(vencimientoUtc).DayNumber : null;

    // Las proyecciones se traducen a SQL: la base devuelve solo estas columnas, no la entidad completa.
    public static readonly Expression<Func<Cobranza, CobranzaResumenDTO>> ComoResumen = c => new CobranzaResumenDTO(
        c.Id,
        c.Numero,
        c.Estado,
        c.MontoTotal,
        c.MontoPagado,
        c.MontoTotal - c.MontoPagado > 0 ? c.MontoTotal - c.MontoPagado : 0m,
        c.FechaEmision,
        c.FechaVencimiento,
        c.FechaPagoCompleto,
        new ClienteResumenDTO(c.Cliente!.Id, c.Cliente.NombreCompleto),
        new UsuarioResumenDTO(c.Promotor!.Id, c.Promotor.NombreCompleto),
        c.HojaPedido == null ? null : new HojaPedidoResumenDTO(c.HojaPedido.Id, c.HojaPedido.Numero),
        c.Pagos.Count(p => !p.Anulado),
        // Depende del día de hoy en Bolivia: lo completa CobranzaService.
        null);

    public static readonly Expression<Func<Cobranza, CobranzaDetalleDTO>> ComoDetalle = c => new CobranzaDetalleDTO(
        c.Id,
        c.Numero,
        c.Estado,
        c.MontoTotal,
        c.MontoPagado,
        c.MontoTotal - c.MontoPagado > 0 ? c.MontoTotal - c.MontoPagado : 0m,
        c.FechaEmision,
        c.FechaVencimiento,
        c.FechaPagoCompleto,
        c.Observaciones,
        new ClienteResumenDTO(c.Cliente!.Id, c.Cliente.NombreCompleto),
        new UsuarioResumenDTO(c.Promotor!.Id, c.Promotor.NombreCompleto),
        c.HojaPedido == null ? null : new HojaPedidoResumenDTO(c.HojaPedido.Id, c.HojaPedido.Numero),
        c.Pagos
            .OrderBy(p => p.FechaPago)
            .Select(p => new PagoDTO(p.Id, p.Monto, p.MetodoPago, p.FechaPago, p.Referencia, p.Observaciones,
                new UsuarioResumenDTO(p.RegistradoPor!.Id, p.RegistradoPor.NombreCompleto), p.Anulado, p.MotivoAnulacion,
                p.EvidenciaId))
            .ToList(),
        null);
}
