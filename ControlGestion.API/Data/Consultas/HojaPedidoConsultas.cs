using System.Linq.Expressions;
using ControlGestion.API.DTOs;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;

namespace ControlGestion.API.Data.Consultas;

public static class HojaPedidoConsultas
{
    /// <summary>
    /// Sin seguridad.ver-cartera-completa, el usuario solo ve las HP de los clientes de su cartera, igual que en
    /// Cobranzas. Si una zona cambia de promotor, el nuevo ve las HP de sus clientes, también las viejas.
    /// </summary>
    public static IQueryable<HojaPedido> VisiblesPara(this IQueryable<HojaPedido> hojas, IUsuarioActual usuario)
    {
        if (usuario.VeCarteraCompleta) return hojas;
        var usuarioId = usuario.Id;
        return hojas.Where(h => h.Cliente!.PromotorId == usuarioId);
    }

    /// <summary>Busca en el número de la HP y en el nombre y el código del cliente.</summary>
    public static IQueryable<HojaPedido> Buscar(this IQueryable<HojaPedido> hojas, string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return hojas;
        var t = texto.Trim();
        return hojas.Where(h => h.Numero.Contains(t) || h.Cliente!.NombreCompleto.Contains(t) || h.Cliente.Codigo.Contains(t));
    }

    public static readonly Expression<Func<HojaPedido, HojaPedidoListaDTO>> ComoLista = h => new HojaPedidoListaDTO(
        h.Id,
        h.Numero,
        h.EstadoHP,
        h.TipoVenta,
        h.Total,
        h.FechaRegistro,
        new ClienteResumenDTO(h.Cliente!.Id, h.Cliente.NombreCompleto),
        new UsuarioResumenDTO(h.Promotor!.Id, h.Promotor.NombreCompleto),
        new ZonaResumenDTO(h.Zona!.Id, h.Zona.Nombre));

    public static readonly Expression<Func<HojaPedido, HojaPedidoDTO>> ComoDetalle = h => new HojaPedidoDTO(
        h.Id,
        h.Numero,
        h.EstadoHP,
        h.TipoVenta,
        h.SubTotal,
        h.Descuento,
        h.Total,
        h.FechaRegistro,
        h.Latitud,
        h.Longitud,
        h.PrecisionGPS,
        new ClienteResumenDTO(h.Cliente!.Id, h.Cliente.NombreCompleto),
        new UsuarioResumenDTO(h.Promotor!.Id, h.Promotor.NombreCompleto),
        new ZonaResumenDTO(h.Zona!.Id, h.Zona.Nombre),
        h.ObservacionesAnulacion,
        h.FechaAnulacion,
        h.AnuladoPor == null ? null : new UsuarioResumenDTO(h.AnuladoPor.Id, h.AnuladoPor.NombreCompleto),
        h.Cobranza == null ? null : new CobranzaReferenciaDTO(h.Cobranza.Id, h.Cobranza.Numero, h.Cobranza.Estado),
        h.Detalles
            .OrderBy(d => d.Id)
            .Select(d => new DetalleHojaPedidoDTO(d.Id, d.ProductoId, d.Producto!.SKU, d.Producto.Descripcion,
                d.Cantidad, d.PrecioUnitario, d.Descuento, d.Subtotal))
            .ToList());

    public static readonly Expression<Func<HojaPedido, HojaParaImprimirDTO>> ParaImprimir = h => new HojaParaImprimirDTO(
        h.Id,
        h.Numero,
        h.EstadoHP,
        h.TipoVenta,
        h.FechaRegistro,
        new ClienteParaImprimirDTO(h.Cliente!.Id, h.Cliente.Codigo, h.Cliente.NombreCompleto, h.Cliente.RazonSocial, h.Cliente.NIT,
            h.Cliente.DireccionTexto, h.Cliente.Telefono),
        new UsuarioResumenDTO(h.Promotor!.Id, h.Promotor.NombreCompleto),
        new ZonaResumenDTO(h.Zona!.Id, h.Zona.Nombre),
        h.Detalles
            .OrderBy(d => d.Id)
            .Select(d => new DetalleHojaPedidoDTO(d.Id, d.ProductoId, d.Producto!.SKU, d.Producto.Descripcion,
                d.Cantidad, d.PrecioUnitario, d.Descuento, d.Subtotal))
            .ToList(),
        h.SubTotal,
        h.Descuento,
        h.Total,
        h.Cobranza == null ? null : new CobranzaParaImprimirDTO(h.Cobranza.Id, h.Cobranza.Numero, h.Cobranza.FechaVencimiento),
        h.FechaAnulacion,
        h.AnuladoPor == null ? null : new UsuarioResumenDTO(h.AnuladoPor.Id, h.AnuladoPor.NombreCompleto),
        h.ObservacionesAnulacion);
}
