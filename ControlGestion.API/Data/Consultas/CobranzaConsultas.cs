using System.Linq.Expressions;
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
