using System.Linq.Expressions;
using ControlGestion.API.Configuracion;
using ControlGestion.API.DTOs;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;

namespace ControlGestion.API.Data.Consultas;

public static class EventoConsultas
{
    /// <summary>
    /// Sin seguridad.ver-cartera-completa, el usuario ve los eventos que registró, los de los clientes de su cartera y
    /// los de los pedidos que puede ver (de su cartera o asignados a él para entregar).
    /// </summary>
    public static IQueryable<Evento> VisiblesPara(this IQueryable<Evento> eventos, IUsuarioActual usuario)
    {
        if (usuario.VeCarteraCompleta) return eventos;
        var usuarioId = usuario.Id;
        return eventos.Where(e => e.PromotorId == usuarioId
            || e.Cliente!.PromotorId == usuarioId
            || e.Pedido!.Cliente!.PromotorId == usuarioId
            || e.Pedido.RepartidorId == usuarioId);
    }

    /// <summary>Busca en el número del evento, en el nombre y el código del cliente y en la descripción.</summary>
    public static IQueryable<Evento> Buscar(this IQueryable<Evento> eventos, string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return eventos;
        var t = texto.Trim();
        return eventos.Where(e => e.Numero.Contains(t) || e.Descripcion.Contains(t)
            || e.Cliente!.NombreCompleto.Contains(t) || e.Cliente.Codigo.Contains(t));
    }

    /// <summary>
    /// Los filtros de la lista, del mapa y del reporte. La zona es la del cliente del evento. Desde y Hasta son días de
    /// Bolivia de la fecha del evento.
    /// </summary>
    public static IQueryable<Evento> Filtrar(this IQueryable<Evento> eventos, FiltrosEventoDTO filtros)
    {
        var consulta = eventos.Buscar(filtros.Buscar);
        if (filtros.TipoEventoId.HasValue) consulta = consulta.Where(e => e.TipoEventoId == filtros.TipoEventoId);
        if (filtros.Estado.HasValue) consulta = consulta.Where(e => e.EstadoEvento == filtros.Estado);
        if (!string.IsNullOrEmpty(filtros.PromotorId)) consulta = consulta.Where(e => e.PromotorId == filtros.PromotorId);
        if (filtros.ZonaId.HasValue) consulta = consulta.Where(e => e.Cliente!.ZonaId == filtros.ZonaId);
        if (filtros.ClienteId.HasValue) consulta = consulta.Where(e => e.ClienteId == filtros.ClienteId);
        if (filtros.Desde is { } desde)
        {
            var inicio = HoraBolivia.InicioDelDiaUtc(desde);
            consulta = consulta.Where(e => e.FechaEvento >= inicio);
        }
        if (filtros.Hasta is { } hasta)
        {
            var fin = HoraBolivia.FinDelDiaUtc(hasta);
            consulta = consulta.Where(e => e.FechaEvento < fin);
        }
        return consulta;
    }

    public static readonly Expression<Func<TipoEvento, TipoEventoDTO>> TipoComoDTO =
        t => new TipoEventoDTO(t.Id, t.Nombre, t.RequiereEvidencia, t.NivelPrioridad);

    public static readonly Expression<Func<Evento, EventoListaDTO>> ComoLista = e => new EventoListaDTO(
        e.Id,
        e.Numero,
        new TipoEventoDTO(e.TipoEvento!.Id, e.TipoEvento.Nombre, e.TipoEvento.RequiereEvidencia, e.TipoEvento.NivelPrioridad),
        e.EstadoEvento,
        e.FechaEvento,
        e.Descripcion,
        e.Cliente == null ? null : new ClienteResumenDTO(e.Cliente.Id, e.Cliente.NombreCompleto),
        e.Pedido == null ? null : new PedidoResumenDTO(e.Pedido.Id, e.Pedido.Numero),
        new UsuarioResumenDTO(e.Promotor!.Id, e.Promotor.NombreCompleto),
        e.Cliente == null ? null : new ZonaResumenDTO(e.Cliente.Zona!.Id, e.Cliente.Zona.Nombre),
        e.Evidencias.Count);

    public static readonly Expression<Func<Evento, EventoDTO>> ComoDetalle = e => new EventoDTO(
        e.Id,
        e.Numero,
        new TipoEventoDTO(e.TipoEvento!.Id, e.TipoEvento.Nombre, e.TipoEvento.RequiereEvidencia, e.TipoEvento.NivelPrioridad),
        e.EstadoEvento,
        e.FechaEvento,
        e.Latitud,
        e.Longitud,
        e.Descripcion,
        e.Cliente == null ? null : new ClienteResumenDTO(e.Cliente.Id, e.Cliente.NombreCompleto),
        e.Pedido == null ? null : new PedidoResumenDTO(e.Pedido.Id, e.Pedido.Numero),
        new UsuarioResumenDTO(e.Promotor!.Id, e.Promotor.NombreCompleto),
        e.Cliente == null ? null : new ZonaResumenDTO(e.Cliente.Zona!.Id, e.Cliente.Zona.Nombre),
        e.Solucion,
        e.FechaResolucion,
        e.ResueltoPor == null ? null : new UsuarioResumenDTO(e.ResueltoPor.Id, e.ResueltoPor.NombreCompleto),
        e.Evidencias
            .OrderBy(f => f.Id)
            .Select(f => new FotoDTO(f.Id, f.NombreArchivo, f.FechaSubida,
                new UsuarioResumenDTO(f.SubidoPor!.Id, f.SubidoPor.NombreCompleto), f.Latitud, f.Longitud, f.FechaCaptura))
            .ToList());
}
