using System.Linq.Expressions;
using ControlGestion.API.DTOs;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;

namespace ControlGestion.API.Data.Consultas;

public static class RutaConsultas
{
    /// <summary>Sin seguridad.ver-cartera-completa, el usuario solo ve sus rutas (7.3.20, ext. 1a).</summary>
    public static IQueryable<Ruta> VisiblesPara(this IQueryable<Ruta> rutas, IUsuarioActual usuario)
    {
        if (usuario.VeCarteraCompleta) return rutas;
        var usuarioId = usuario.Id;
        return rutas.Where(r => r.PromotorId == usuarioId);
    }

    public static readonly Expression<Func<Ruta, RutaListaDTO>> ComoLista = r => new RutaListaDTO(
        r.Id,
        r.Numero,
        r.FechaPlanificada,
        r.Estado,
        new UsuarioResumenDTO(r.Promotor!.Id, r.Promotor.NombreCompleto),
        new ZonaResumenDTO(r.Zona!.Id, r.Zona.Nombre),
        r.FechaInicio,
        r.FechaFin,
        r.Detalles.Count(),
        r.Detalles.Count(d => d.EstadoVisita != EstadoVisita.Pendiente),
        r.Detalles.Count(d => d.EstadoVisita == EstadoVisita.Visitado));

    public static readonly Expression<Func<Ruta, RutaDTO>> ComoDetalle = r => new RutaDTO(
        r.Id,
        r.Numero,
        r.FechaPlanificada,
        r.Estado,
        new UsuarioResumenDTO(r.Promotor!.Id, r.Promotor.NombreCompleto),
        new ZonaResumenDTO(r.Zona!.Id, r.Zona.Nombre),
        r.FechaInicio,
        r.FechaFin,
        r.Detalles.Count(),
        r.Detalles.Count(d => d.EstadoVisita != EstadoVisita.Pendiente),
        r.Detalles.Count(d => d.EstadoVisita == EstadoVisita.Visitado),
        r.Detalles.Count(d => d.EstadoVisita == EstadoVisita.Pendiente),
        r.Detalles.OrderBy(d => d.Orden).Select(d => new VisitaDTO(
            d.Id,
            d.Orden,
            d.EstadoVisita,
            d.HoraVisita,
            d.Latitud,
            d.Longitud,
            d.Observaciones,
            new ClienteDeRutaDTO(d.Cliente!.Id, d.Cliente.Codigo, d.Cliente.NombreCompleto, d.Cliente.DireccionTexto,
                d.Cliente.Latitud, d.Cliente.Longitud, d.Cliente.UltimaVisita, d.Cliente.ProximaVisita),
            // Depende del día en que se pide: lo completa RutaService.
            null)).ToList());
}
