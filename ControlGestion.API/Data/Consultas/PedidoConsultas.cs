using System.Linq.Expressions;
using ControlGestion.API.DTOs;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;

namespace ControlGestion.API.Data.Consultas;

public static class PedidoConsultas
{
    /// <summary>
    /// Sin seguridad.ver-cartera-completa, el usuario ve los pedidos de los clientes de su cartera y los que tiene
    /// asignados para entregar, aunque el cliente sea de otra cartera.
    /// </summary>
    public static IQueryable<Pedido> VisiblesPara(this IQueryable<Pedido> pedidos, IUsuarioActual usuario)
    {
        if (usuario.VeCarteraCompleta) return pedidos;
        var usuarioId = usuario.Id;
        return pedidos.Where(p => p.Cliente!.PromotorId == usuarioId || p.RepartidorId == usuarioId);
    }

    /// <summary>Busca en el número del pedido y en el nombre y el código del cliente.</summary>
    public static IQueryable<Pedido> Buscar(this IQueryable<Pedido> pedidos, string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return pedidos;
        var t = texto.Trim();
        return pedidos.Where(p => p.Numero.Contains(t) || p.Cliente!.NombreCompleto.Contains(t) || p.Cliente.Codigo.Contains(t));
    }

    public static readonly Expression<Func<Pedido, PedidoListaDTO>> ComoLista = p => new PedidoListaDTO(
        p.Id,
        p.Numero,
        new EstadoPedidoDTO(p.EstadoPedido!.Id, p.EstadoPedido.Nombre),
        p.Total,
        p.FechaRegistro,
        p.FechaEntregaEstimada,
        new ClienteResumenDTO(p.Cliente!.Id, p.Cliente.NombreCompleto),
        new UsuarioResumenDTO(p.Promotor!.Id, p.Promotor.NombreCompleto),
        p.Repartidor == null ? null : new UsuarioResumenDTO(p.Repartidor.Id, p.Repartidor.NombreCompleto),
        new ZonaResumenDTO(p.Zona!.Id, p.Zona.Nombre));

    public static readonly Expression<Func<HistorialEstadoPedido, HistorialPedidoDTO>> HistorialComoDTO = h => new HistorialPedidoDTO(
        h.Id,
        new EstadoPedidoDTO(h.EstadoPedido!.Id, h.EstadoPedido.Nombre),
        h.FechaCambio,
        new UsuarioResumenDTO(h.Usuario!.Id, h.Usuario.NombreCompleto),
        h.Observacion,
        h.LatitudCambio,
        h.LongitudCambio);

    public static readonly Expression<Func<Pedido, PedidoDTO>> ComoDetalle = p => new PedidoDTO(
        p.Id,
        p.Numero,
        new EstadoPedidoDTO(p.EstadoPedido!.Id, p.EstadoPedido.Nombre),
        p.SubTotal,
        p.Descuento,
        p.Total,
        p.Observaciones,
        p.FechaRegistro,
        p.FechaEntregaEstimada,
        p.FechaEntregaReal,
        p.LatitudEntrega,
        p.LongitudEntrega,
        new ClienteResumenDTO(p.Cliente!.Id, p.Cliente.NombreCompleto),
        new UsuarioResumenDTO(p.Promotor!.Id, p.Promotor.NombreCompleto),
        p.Repartidor == null ? null : new UsuarioResumenDTO(p.Repartidor.Id, p.Repartidor.NombreCompleto),
        new ZonaResumenDTO(p.Zona!.Id, p.Zona.Nombre),
        p.HojaPedido == null ? null : new HojaPedidoResumenDTO(p.HojaPedido.Id, p.HojaPedido.Numero),
        p.Detalles
            .OrderBy(d => d.Id)
            .Select(d => new DetallePedidoDTO(d.Id, d.ProductoId, d.Producto!.SKU, d.Producto.Descripcion,
                d.Cantidad, d.PrecioUnitario, d.Descuento, d.Subtotal, d.CantidadEntregada))
            .ToList(),
        p.Historial.AsQueryable().OrderBy(h => h.Id).Select(HistorialComoDTO).ToList());
}
