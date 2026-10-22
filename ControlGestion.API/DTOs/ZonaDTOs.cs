using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

/// <summary>
/// Una zona de venta (7.2). PromotorValido: el promotor está activo y recorre rutas; si no, la zona se marca para que
/// el gerente general la reasigne. Clientes: los de la zona que el usuario puede ver (su cartera, o todos).
/// </summary>
public record ZonaDTO(int Id, string Codigo, string Nombre, UsuarioResumenDTO? Promotor, bool PromotorValido, int Clientes);

/// <summary>Lo que se cambia de una zona: el código no se modifica (7.2).</summary>
public record ModificarZonaDTO
{
    [Required(ErrorMessage = "Escribe el nombre de la zona.")]
    [MaxLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Obligatorio: un usuario activo que recorre rutas (lo revisa el servicio).</summary>
    [Required(ErrorMessage = "Elige el promotor de la zona.")]
    public string PromotorId { get; init; } = string.Empty;
}

public record RegistrarZonaDTO : ModificarZonaDTO
{
    [Required(ErrorMessage = "Escribe el código de la zona.")]
    [MaxLength(20, ErrorMessage = "El código no puede superar los 20 caracteres.")]
    public string Codigo { get; init; } = string.Empty;
}
