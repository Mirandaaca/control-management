using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>Zonas de venta (7.2): cada una con su promotor, que es el de todos sus clientes.</summary>
public class ZonaService(AppDbContext db, IUsuarioActual usuario)
{
    // Se filtra antes de proyectar: EF no puede filtrar sobre el constructor de un record.
    private IQueryable<ZonaDTO> Proyectar(IQueryable<Zona> zonas)
    {
        // Los clientes que el usuario puede ver: un promotor no ve cuántos clientes tiene la cartera de otro.
        var clientesVisibles = db.Clientes.VisiblesPara(usuario);
        // La misma regla que UsuarioConsultas.ConPermiso, escrita aquí para que la base la resuelva en la misma consulta.
        const string recorrerRuta = Permisos.Territorial.RecorrerRuta;
        return zonas.Select(z => new ZonaDTO(
            z.Id,
            z.Codigo,
            z.Nombre,
            z.Promotor == null ? null : new UsuarioResumenDTO(z.Promotor.Id, z.Promotor.NombreCompleto),
            z.Promotor != null && z.Promotor.Activo && db.UserRoles.Any(ur => ur.UserId == z.PromotorId
                && db.RolesPermisos.Any(rp => rp.RolId == ur.RoleId && rp.Permiso!.Clave == recorrerRuta)),
            clientesVisibles.Count(c => c.ZonaId == z.Id)));
    }

    public Task<List<ZonaDTO>> ListarAsync() =>
        Proyectar(db.Zonas.OrderBy(z => z.Nombre)).ToListAsync();

    public async Task<ZonaDTO> ObtenerAsync(int id) =>
        await Proyectar(db.Zonas.Where(z => z.Id == id)).FirstOrDefaultAsync()
        ?? throw new NoEncontradoException("No se encontró la zona.");

    public async Task<ZonaDTO> RegistrarAsync(RegistrarZonaDTO dto)
    {
        var zona = new Zona { Codigo = dto.Codigo.Trim(), Nombre = dto.Nombre.Trim(), PromotorId = dto.PromotorId };
        db.Zonas.Add(zona);
        await db.SaveChangesAsync();
        return await ObtenerAsync(zona.Id);
    }

    /// <summary>
    /// Cambia el nombre y el promotor. El cliente siempre tiene el promotor de su zona (7.2): si cambia, sus clientes
    /// pasan al nuevo en la misma transacción. Las HP, cobranzas y pedidos ya registrados conservan el suyo.
    /// </summary>
    public async Task<ZonaDTO> ModificarAsync(int id, ModificarZonaDTO dto)
    {
        var zona = await db.Zonas.FindAsync(id) ?? throw new NoEncontradoException("No se encontró la zona.");
        var cambiaElPromotor = zona.PromotorId != dto.PromotorId;

        await using var transaccion = await db.Database.BeginTransactionAsync();
        zona.Nombre = dto.Nombre.Trim();
        zona.PromotorId = dto.PromotorId;
        await db.SaveChangesAsync();
        if (cambiaElPromotor)
            await db.Clientes.Where(c => c.ZonaId == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.PromotorId, dto.PromotorId));
        await transaccion.CommitAsync();
        return await ObtenerAsync(id);
    }

    public async Task EliminarAsync(int id)
    {
        var zona = await db.Zonas.FindAsync(id) ?? throw new NoEncontradoException("No se encontró la zona.");
        db.Zonas.Remove(zona);
        await db.SaveChangesAsync();
    }
}
