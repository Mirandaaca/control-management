using ControlGestion.API.Data;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>
/// La migración CategoriasDeProductos (tarea 29), que convierte la categoría de texto en la tabla Categorias. Baja la base
/// de la prueba a la migración anterior, pone los textos como podían estar y la vuelve a subir: las pruebas de la
/// colección "Api" corren de a una, así ninguna la ve a medio camino, y el finally la deja siempre al día.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class CategoriasDeProductosTests(ApiDePrueba api)
{
    private const string MigracionAnterior = "20261030233348_NombreDisponibleParaRevisita";

    [Fact]
    public async Task Cada_texto_distinto_es_una_categoria_y_los_productos_sin_texto_quedan_en_Sin_categoria()
    {
        using var operador = await api.OperadorAsync();
        var ids = new List<int>();
        for (var i = 0; i < 5; i++) ids.Add(await operador.CrearProductoAsync(10m, stockInicial: 0));
        // El mismo texto con otras mayúsculas, con espacios y con tilde; uno vacío y uno sin texto.
        var nombre = Ayudantes.Unico("T29 Limpieza");
        string?[] textos = [nombre, $"  {nombre.ToLowerInvariant()} ", nombre.ToUpperInvariant().Replace("LIMPIEZA", "LÍMPIEZA"), "", null];

        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var migrador = db.GetService<IMigrator>();
        string? devuelto;
        try
        {
            await migrador.MigrateAsync(MigracionAnterior);
            // El Down le devolvió a cada producto el nombre de su categoría como texto.
            devuelto = await db.Database.SqlQuery<string>($"SELECT Categoria AS Value FROM Productos WHERE Id = {ids[0]}")
                .SingleAsync();
            for (var i = 0; i < ids.Count; i++)
            {
                if (textos[i] is { } texto)
                    await db.Database.ExecuteSqlAsync($"UPDATE Productos SET Categoria = {texto} WHERE Id = {ids[i]}");
                else
                    await db.Database.ExecuteSqlAsync($"UPDATE Productos SET Categoria = NULL WHERE Id = {ids[i]}");
            }
        }
        finally
        {
            await migrador.MigrateAsync();
            await api.VolverALeerLaCategoriaAsync();
        }

        Assert.Equal(ApiDePrueba.NombreDeLaCategoriaDePruebas, devuelto);
        var categorias = await db.Productos.AsNoTracking().Where(p => ids.Contains(p.Id)).OrderBy(p => p.Id)
            .Select(p => new { p.CategoriaId, p.Categoria!.Nombre }).ToListAsync();
        // Una sola categoría para los tres, con el nombre como lo escribió el producto más antiguo.
        Assert.Equal([nombre, nombre, nombre, "Sin categoría", "Sin categoría"], categorias.Select(c => c.Nombre));
        Assert.Single(categorias.Take(3).Select(c => c.CategoriaId).Distinct());
        Assert.Equal(1, await db.Categorias.CountAsync(c => c.Nombre == nombre));
    }
}
