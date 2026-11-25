using System.Linq.Expressions;
using ControlGestion.API.Data;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>
/// Los tipos de evento (7.3.13, extensión nueva de la tarea 32). El nombre no se repite, sin importar mayúsculas ni
/// tildes. Uno inactivo no se elige en un evento nuevo, pero los eventos que ya lo tienen lo conservan y se siguen
/// filtrando por él. Uno con eventos no se elimina: se desactiva. Siempre queda al menos uno activo, para que se puedan
/// registrar eventos.
/// </summary>
public class TipoEventoService(AppDbContext db)
{
    private Expression<Func<TipoEvento, TipoEventoAdminDTO>> ComoAdmin =>
        t => new TipoEventoAdminDTO(t.Id, t.Nombre, t.RequiereEvidencia, t.NivelPrioridad, t.Activo,
            db.Eventos.Count(e => e.TipoEventoId == t.Id));

    /// <summary>Los activos primero, cada grupo en el orden en que se registraron (el de los formularios).</summary>
    public Task<List<TipoEventoAdminDTO>> ListarAsync() =>
        db.TiposEvento.OrderByDescending(t => t.Activo).ThenBy(t => t.Id).Select(ComoAdmin).ToListAsync();

    /// <summary>
    /// Para los filtros de Eventos, el Mapa y el Reporte: los activos y los inactivos con eventos, para seguir encontrando
    /// los eventos viejos. Uno inactivo sin eventos no encontraría nada.
    /// </summary>
    public Task<List<TipoEventoOpcionDTO>> OpcionesAsync() =>
        db.TiposEvento
            .Where(t => t.Activo || db.Eventos.Any(e => e.TipoEventoId == t.Id))
            .OrderByDescending(t => t.Activo).ThenBy(t => t.Id)
            .Select(t => new TipoEventoOpcionDTO(t.Id, t.Nombre, t.Activo))
            .ToListAsync();

    public async Task<TipoEventoAdminDTO> ObtenerAsync(int id) =>
        await db.TiposEvento.Where(t => t.Id == id).Select(ComoAdmin).FirstOrDefaultAsync()
        ?? throw new NoEncontradoException("No se encontró el tipo de evento.");

    public async Task<TipoEventoAdminDTO> RegistrarAsync(GuardarTipoEventoDTO dto)
    {
        var tipo = new TipoEvento
        {
            Nombre = dto.Nombre.Trim(),
            RequiereEvidencia = dto.RequiereEvidencia,
            NivelPrioridad = dto.NivelPrioridad!.Value,
            Activo = true
        };
        await ExigirNombreLibreAsync(tipo.Nombre, idPropio: null);
        db.TiposEvento.Add(tipo);
        await db.SaveChangesAsync();
        return await ObtenerAsync(tipo.Id);
    }

    /// <summary>
    /// El nombre nuevo se ve también en los eventos ya registrados. "Pide foto" vale para los pendientes cuando se
    /// resuelven: la regla se revisa al resolver (ext. 9), y los resueltos no cambian.
    /// </summary>
    public async Task<TipoEventoAdminDTO> ModificarAsync(int id, GuardarTipoEventoDTO dto)
    {
        var tipo = await BuscarAsync(id);
        var nombre = dto.Nombre.Trim();
        await ExigirNombreLibreAsync(nombre, id);
        tipo.Nombre = nombre;
        tipo.RequiereEvidencia = dto.RequiereEvidencia;
        tipo.NivelPrioridad = dto.NivelPrioridad!.Value;
        await db.SaveChangesAsync();
        return await ObtenerAsync(id);
    }

    public async Task<TipoEventoAdminDTO> ActivarAsync(int id)
    {
        var tipo = await BuscarAsync(id);
        tipo.Activo = true;
        await db.SaveChangesAsync();
        return await ObtenerAsync(id);
    }

    public async Task<TipoEventoAdminDTO> DesactivarAsync(int id)
    {
        var tipo = await BuscarAsync(id);
        if (tipo.Activo)
        {
            await ExigirOtroActivoAsync(tipo, "desactivar");
            tipo.Activo = false;
            await db.SaveChangesAsync();
        }
        return await ObtenerAsync(id);
    }

    public async Task EliminarAsync(int id)
    {
        var tipo = await BuscarAsync(id);

        var eventos = await db.Eventos.CountAsync(e => e.TipoEventoId == id);
        if (eventos > 0)
            throw new ConflictoException("tipo_evento_en_uso", eventos == 1
                ? $"{tipo.Nombre} tiene 1 evento: desactívalo en lugar de eliminarlo."
                : $"{tipo.Nombre} tiene {eventos} eventos: desactívalo en lugar de eliminarlo.");
        if (tipo.Activo) await ExigirOtroActivoAsync(tipo, "eliminar");

        db.TiposEvento.Remove(tipo);
        await db.SaveChangesAsync();
    }

    private async Task<TipoEvento> BuscarAsync(int id) =>
        await db.TiposEvento.FindAsync(id) ?? throw new NoEncontradoException("No se encontró el tipo de evento.");

    // Sin ningún tipo activo, la app no podría registrar eventos (7.3.13, paso 2.2).
    private async Task ExigirOtroActivoAsync(TipoEvento tipo, string accion)
    {
        if (!await db.TiposEvento.AnyAsync(t => t.Activo && t.Id != tipo.Id))
            throw new ReglaDeNegocioException("ultimo_tipo_activo",
                $"{tipo.Nombre} es el único tipo de evento activo: sin él no se podrían registrar eventos. Activa o registra otro antes de {accion}lo.");
    }

    // La intercalación de la base no distingue mayúsculas ni tildes: "otro" choca con "Otro". El índice único frena además
    // dos registros a la vez. El mismo tipo puede cambiar solo sus mayúsculas.
    private async Task ExigirNombreLibreAsync(string nombre, int? idPropio)
    {
        if (await db.TiposEvento.AnyAsync(t => t.Nombre == nombre && t.Id != idPropio))
            throw new ConflictoException("tipo_evento_repetido", $"Ya existe el tipo de evento \"{nombre}\".");
    }
}
