using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

/// <summary>
/// Un estado en la pantalla de administración (7.4). Protegido: no se elimina ni se renombra (Activo, Disponible para
/// re-visita e Inactivo). EsDelSistema: además, su color es fijo. Clientes: cuántos lo tienen ahora.
/// </summary>
public record EstadoClienteAdminDTO(int Id, string Nombre, string Color, string? Descripcion, bool EsDelSistema, bool Protegido,
    int Clientes);

/// <summary>El color se elige de la paleta (PaletaDeEstados.Manuales); lo revisa el servicio, porque el de un estado del sistema es fijo.</summary>
public record GuardarEstadoClienteDTO
{
    [Required(ErrorMessage = "Escribe el nombre del estado.")]
    [MaxLength(50, ErrorMessage = "El nombre no puede superar los 50 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    [Required(ErrorMessage = "Elige un color de la paleta.")]
    public string Color { get; init; } = string.Empty;

    [MaxLength(200, ErrorMessage = "La descripción no puede superar los 200 caracteres.")]
    public string? Descripcion { get; init; }
}
