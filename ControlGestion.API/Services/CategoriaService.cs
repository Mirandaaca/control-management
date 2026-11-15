using System.Linq.Expressions;
using ControlGestion.API.Data;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>
/// Las categorías de los productos (7.3.29, tarea 29). El nombre no se repite, sin importar mayúsculas ni tildes, y una
/// categoría con productos no se elimina: tampoco si solo tiene inactivos, que conservan su categoría (ext. 5).
/// </summary>
public class CategoriaService(AppDbContext db)
{
    private static readonly Expression<Func<Categoria, CategoriaAdminDTO>> ComoAdmin = c => new CategoriaAdminDTO(
        c.Id, c.Nombre, c.Productos.Count(p => p.Activo), c.Productos.Count(p => !p.Activo));

    public Task<List<CategoriaAdminDTO>> ListarAsync() =>
        db.Categorias.OrderBy(c => c.Nombre).Select(ComoAdmin).ToListAsync();

    /// <summary>Para elegir la categoría de un producto y para los filtros de Productos e Inventario.</summary>
    public Task<List<CategoriaResumenDTO>> OpcionesAsync() =>
        db.Categorias.OrderBy(c => c.Nombre).Select(c => new CategoriaResumenDTO(c.Id, c.Nombre)).ToListAsync();

    public async Task<CategoriaAdminDTO> ObtenerAsync(int id) =>
        await db.Categorias.Where(c => c.Id == id).Select(ComoAdmin).FirstOrDefaultAsync()
        ?? throw new NoEncontradoException("No se encontró la categoría.");

    public async Task<CategoriaAdminDTO> RegistrarAsync(GuardarCategoriaDTO dto)
    {
        var categoria = new Categoria { Nombre = dto.Nombre.Trim() };
        await ExigirNombreLibreAsync(categoria.Nombre, idPropio: null);
        db.Categorias.Add(categoria);
        await db.SaveChangesAsync();
        return await ObtenerAsync(categoria.Id);
    }

    public async Task<CategoriaAdminDTO> ModificarAsync(int id, GuardarCategoriaDTO dto)
    {
        var categoria = await db.Categorias.FindAsync(id) ?? throw new NoEncontradoException("No se encontró la categoría.");
        var nombre = dto.Nombre.Trim();
        await ExigirNombreLibreAsync(nombre, id);
        categoria.Nombre = nombre;
        await db.SaveChangesAsync();
        return await ObtenerAsync(id);
    }

    public async Task EliminarAsync(int id)
    {
        var categoria = await db.Categorias.FindAsync(id) ?? throw new NoEncontradoException("No se encontró la categoría.");

        var activos = await db.Productos.CountAsync(p => p.CategoriaId == id && p.Activo);
        if (activos > 0)
            throw new ConflictoException("categoria_en_uso", activos == 1
                ? $"{categoria.Nombre} tiene 1 producto: pásalo a otra categoría antes de eliminarla."
                : $"{categoria.Nombre} tiene {activos} productos: pásalos a otra categoría antes de eliminarla.");

        // Los inactivos no se ven en Productos ni se editan: por eso el mensaje dice por qué no se puede.
        var inactivos = await db.Productos.CountAsync(p => p.CategoriaId == id && !p.Activo);
        if (inactivos > 0)
            throw new ConflictoException("categoria_en_uso", inactivos == 1
                ? $"{categoria.Nombre} tiene 1 producto inactivo, que conserva su categoría: no se puede eliminar."
                : $"{categoria.Nombre} tiene {inactivos} productos inactivos, que conservan su categoría: no se puede eliminar.");

        db.Categorias.Remove(categoria);
        await db.SaveChangesAsync();
    }

    // La intercalación de la base no distingue mayúsculas ni tildes: "limpieza" choca con "Limpieza". El índice único frena
    // además dos registros a la vez. La misma categoría puede cambiar solo sus mayúsculas.
    private async Task ExigirNombreLibreAsync(string nombre, int? idPropio)
    {
        if (await db.Categorias.AnyAsync(c => c.Nombre == nombre && c.Id != idPropio))
            throw new ConflictoException("categoria_repetida", $"Ya existe la categoría \"{nombre}\".");
    }
}
