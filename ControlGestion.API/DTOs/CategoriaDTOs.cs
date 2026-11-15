using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

/// <summary>La categoría de un producto, y cada opción para elegirla o filtrar por ella (tarea 29).</summary>
public record CategoriaResumenDTO(int Id, string Nombre);

/// <summary>
/// Una categoría en su pantalla (7.3.29, tarea 29). Productos son los activos, los que se ven en Productos; Inactivos,
/// los que la conservan aunque ya no se vendan. Con alguno de los dos, la categoría no se elimina.
/// </summary>
public record CategoriaAdminDTO(int Id, string Nombre, int Productos, int Inactivos);

public record GuardarCategoriaDTO
{
    [Required(ErrorMessage = "Escribe el nombre de la categoría.")]
    [MaxLength(80, ErrorMessage = "El nombre no puede superar los 80 caracteres.")]
    public string Nombre { get; init; } = string.Empty;
}
