using System.Linq.Expressions;
using ControlGestion.API.Configuracion;
using ControlGestion.API.DTOs;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;

namespace ControlGestion.API.Data.Consultas;

public static class ClienteConsultas
{
    /// <summary>Sin seguridad.ver-cartera-completa, el usuario solo ve los clientes de su cartera (el promotor del cliente es él).</summary>
    public static IQueryable<Cliente> VisiblesPara(this IQueryable<Cliente> clientes, IUsuarioActual usuario)
    {
        if (usuario.VeCarteraCompleta) return clientes;
        var usuarioId = usuario.Id;
        return clientes.Where(c => c.PromotorId == usuarioId);
    }

    /// <summary>
    /// Busca en nombre, código, celular, razón social y NIT. La comparación la hace MySQL con la intercalación de la base
    /// (utf8mb4_0900_ai_ci): no distingue mayúsculas ni tildes.
    /// </summary>
    public static IQueryable<Cliente> Buscar(this IQueryable<Cliente> clientes, string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return clientes;
        var t = texto.Trim();
        return clientes.Where(c => c.NombreCompleto.Contains(t) || c.Codigo.Contains(t) || c.Telefono.Contains(t)
            || (c.RazonSocial != null && c.RazonSocial.Contains(t)) || (c.NIT != null && c.NIT.Contains(t)));
    }

    /// <summary>
    /// La regla de "la próxima visita llegó" (5.2, 5.3), por día de Bolivia y no por hora: un cliente que toca visitar hoy
    /// a las 18:00 ya está disponible y por visitar desde la mañana. Un cliente sin visitas también. Recibe el fin de hoy
    /// en UTC (HoraBolivia.FinDeHoyUtc). <see cref="ProximaVisitaPendiente"/> es exactamente lo contrario.
    /// </summary>
    public static Expression<Func<Cliente, bool>> ProximaVisitaLlego(DateTime finDeHoyUtc) =>
        c => c.ProximaVisita == null || c.ProximaVisita < finDeHoyUtc;

    /// <summary>La próxima visita es de mañana en adelante: el cliente sigue activo.</summary>
    public static Expression<Func<Cliente, bool>> ProximaVisitaPendiente(DateTime finDeHoyUtc) =>
        c => c.ProximaVisita >= finDeHoyUtc;

    /// <summary>
    /// Los clientes por visitar (5.3) al terminar ese día de Bolivia: los del ciclo de visitas (los estados asignados a mano,
    /// como Inactivo, lo sacan) cuya próxima visita llegó o que nunca se visitaron. Lo usan la lista por visitar, el filtro
    /// de Clientes y la ruta sugerida (5.4), que lo pide al día de la ruta.
    /// </summary>
    public static IQueryable<Cliente> PorVisitar(this IQueryable<Cliente> clientes, DateTime finDelDiaUtc) =>
        clientes.Where(c => c.EstadoCliente!.EsDelSistema).Where(ProximaVisitaLlego(finDelDiaUtc));

    /// <summary>Primero los nunca visitados y después del más atrasado al más reciente.</summary>
    public static IOrderedQueryable<Cliente> DelMasAtrasado(this IQueryable<Cliente> clientes) =>
        clientes.OrderBy(c => c.ProximaVisita != null).ThenBy(c => c.ProximaVisita).ThenBy(c => c.NombreCompleto).ThenBy(c => c.Id);

    /// <summary>Los días que pasaron desde el día de la próxima visita hasta ese día, en Bolivia; null si nunca se visitó.</summary>
    public static int? DiasDeAtraso(DateTime? proximaVisitaUtc, DateOnly dia) =>
        proximaVisitaUtc is { } proxima ? dia.DayNumber - HoraBolivia.DiaDe(proxima).DayNumber : null;

    public static readonly Expression<Func<EstadoCliente, EstadoClienteDTO>> EstadoComoDTO =
        e => new EstadoClienteDTO(e.Id, e.Nombre, e.Color, e.Descripcion, e.EsDelSistema);

    public static readonly Expression<Func<Cliente, ClienteListaDTO>> ComoLista = c => new ClienteListaDTO(
        c.Id,
        c.Codigo,
        c.NombreCompleto,
        c.Telefono,
        c.DireccionTexto,
        new ZonaResumenDTO(c.Zona!.Id, c.Zona.Nombre),
        new EstadoClienteDTO(c.EstadoCliente!.Id, c.EstadoCliente.Nombre, c.EstadoCliente.Color, c.EstadoCliente.Descripcion,
            c.EstadoCliente.EsDelSistema),
        c.Promotor == null ? null : new UsuarioResumenDTO(c.Promotor.Id, c.Promotor.NombreCompleto),
        c.SaldoPendiente,
        c.UltimaVisita,
        c.ProximaVisita);

    public static readonly Expression<Func<Cliente, ClienteDTO>> ComoDetalle = c => new ClienteDTO(
        c.Id,
        c.Codigo,
        c.NombreCompleto,
        c.RazonSocial,
        c.NIT,
        c.Telefono,
        c.Email,
        c.DireccionTexto,
        c.Latitud,
        c.Longitud,
        new ZonaResumenDTO(c.Zona!.Id, c.Zona.Nombre),
        new EstadoClienteDTO(c.EstadoCliente!.Id, c.EstadoCliente.Nombre, c.EstadoCliente.Color, c.EstadoCliente.Descripcion,
            c.EstadoCliente.EsDelSistema),
        c.Promotor == null ? null : new UsuarioResumenDTO(c.Promotor.Id, c.Promotor.NombreCompleto),
        c.SaldoPendiente,
        c.UltimaVisita,
        c.ProximaVisita,
        c.FechaCreacion);

    public static readonly Expression<Func<Cliente, ClienteOpcionDTO>> ComoOpcion =
        c => new ClienteOpcionDTO(c.Id, c.Codigo, c.NombreCompleto, c.ZonaId, c.DireccionTexto,
            new EstadoClienteDTO(c.EstadoCliente!.Id, c.EstadoCliente.Nombre, c.EstadoCliente.Color, c.EstadoCliente.Descripcion,
                c.EstadoCliente.EsDelSistema),
            c.EstadoClienteId == EstadosDeCliente.Inactivo);

    public static readonly Expression<Func<Cliente, ClienteMapaDTO>> ComoMapa = c => new ClienteMapaDTO(
        c.Id,
        c.Codigo,
        c.NombreCompleto,
        c.Latitud,
        c.Longitud,
        c.Telefono,
        c.DireccionTexto,
        c.ZonaId,
        c.UltimaVisita,
        c.ProximaVisita,
        new EstadoClienteDTO(c.EstadoCliente!.Id, c.EstadoCliente.Nombre, c.EstadoCliente.Color, c.EstadoCliente.Descripcion,
            c.EstadoCliente.EsDelSistema));
}
