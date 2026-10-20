using System.Linq.Expressions;
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

    public static readonly Expression<Func<EstadoCliente, EstadoClienteDTO>> EstadoComoDTO =
        e => new EstadoClienteDTO(e.Id, e.Nombre, e.Color, e.Descripcion);

    public static readonly Expression<Func<Cliente, ClienteListaDTO>> ComoLista = c => new ClienteListaDTO(
        c.Id,
        c.Codigo,
        c.NombreCompleto,
        c.Telefono,
        new ZonaResumenDTO(c.Zona!.Id, c.Zona.Nombre),
        new EstadoClienteDTO(c.EstadoCliente!.Id, c.EstadoCliente.Nombre, c.EstadoCliente.Color, c.EstadoCliente.Descripcion),
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
        new EstadoClienteDTO(c.EstadoCliente!.Id, c.EstadoCliente.Nombre, c.EstadoCliente.Color, c.EstadoCliente.Descripcion),
        c.Promotor == null ? null : new UsuarioResumenDTO(c.Promotor.Id, c.Promotor.NombreCompleto),
        c.SaldoPendiente,
        c.UltimaVisita,
        c.ProximaVisita,
        c.FechaCreacion);

    public static readonly Expression<Func<Cliente, ClienteOpcionDTO>> ComoOpcion =
        c => new ClienteOpcionDTO(c.Id, c.Codigo, c.NombreCompleto, c.ZonaId);

    public static readonly Expression<Func<Cliente, ClienteMapaDTO>> ComoMapa = c => new ClienteMapaDTO(
        c.Id,
        c.Codigo,
        c.NombreCompleto,
        c.Latitud,
        c.Longitud,
        c.Telefono,
        c.DireccionTexto,
        c.ProximaVisita,
        new EstadoClienteDTO(c.EstadoCliente!.Id, c.EstadoCliente.Nombre, c.EstadoCliente.Color, c.EstadoCliente.Descripcion));
}
