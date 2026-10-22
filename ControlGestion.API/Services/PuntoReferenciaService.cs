using System.Linq.Expressions;
using ControlGestion.API.Data;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>Los puntos de referencia de las zonas (5.5), como mercados: los registra el gerente general y los ven todos.</summary>
public class PuntoReferenciaService(AppDbContext db)
{
    private static readonly Expression<Func<PuntoReferencia, PuntoReferenciaDTO>> ComoDTO = p => new PuntoReferenciaDTO(
        p.Id, new ZonaResumenDTO(p.Zona!.Id, p.Zona.Nombre), p.Nombre, p.Descripcion, p.Latitud, p.Longitud);

    public Task<List<PuntoReferenciaDTO>> ListarAsync(int? zonaId)
    {
        var consulta = db.PuntosReferencia.AsQueryable();
        if (zonaId.HasValue) consulta = consulta.Where(p => p.ZonaId == zonaId);
        return consulta.OrderBy(p => p.Zona!.Nombre).ThenBy(p => p.Nombre).Select(ComoDTO).ToListAsync();
    }

    public async Task<PuntoReferenciaDTO> ObtenerAsync(int id) =>
        await db.PuntosReferencia.Where(p => p.Id == id).Select(ComoDTO).FirstOrDefaultAsync()
        ?? throw new NoEncontradoException("No se encontró el punto de referencia.");

    public async Task<PuntoReferenciaDTO> RegistrarAsync(GuardarPuntoReferenciaDTO dto)
    {
        var punto = new PuntoReferencia();
        await CopiarAsync(dto, punto);
        db.PuntosReferencia.Add(punto);
        await db.SaveChangesAsync();
        return await ObtenerAsync(punto.Id);
    }

    /// <summary>Cambia sus datos; también puede pasar a otra zona.</summary>
    public async Task<PuntoReferenciaDTO> ModificarAsync(int id, GuardarPuntoReferenciaDTO dto)
    {
        var punto = await db.PuntosReferencia.FindAsync(id) ?? throw new NoEncontradoException("No se encontró el punto de referencia.");
        await CopiarAsync(dto, punto);
        await db.SaveChangesAsync();
        return await ObtenerAsync(id);
    }

    public async Task EliminarAsync(int id)
    {
        var punto = await db.PuntosReferencia.FindAsync(id) ?? throw new NoEncontradoException("No se encontró el punto de referencia.");
        db.PuntosReferencia.Remove(punto);
        await db.SaveChangesAsync();
    }

    private async Task CopiarAsync(GuardarPuntoReferenciaDTO dto, PuntoReferencia punto)
    {
        if (!await db.Zonas.AnyAsync(z => z.Id == dto.ZonaId))
            throw new ValidacionException(new Dictionary<string, string[]> { ["zonaId"] = ["La zona elegida no existe."] });
        punto.ZonaId = dto.ZonaId;
        punto.Nombre = dto.Nombre.Trim();
        punto.Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim();
        punto.Latitud = dto.Latitud!.Value;
        punto.Longitud = dto.Longitud!.Value;
    }
}
