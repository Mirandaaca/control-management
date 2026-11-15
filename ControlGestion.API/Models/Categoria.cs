namespace ControlGestion.API.Models;

/// <summary>
/// La categoría de los productos (7.3.29, tarea 29). El nombre no se repite, sin importar mayúsculas ni tildes (la
/// intercalación de la base). Una categoría con productos, aunque estén inactivos, no se elimina.
/// </summary>
public class Categoria
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    public List<Producto> Productos { get; set; } = [];
}
