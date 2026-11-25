using System.ComponentModel.DataAnnotations;
using ControlGestion.API.Models;

namespace ControlGestion.API.DTOs;

/// <summary>
/// Un tipo de evento en su pantalla (7.3.13, tarea 32), con cuántos eventos lo tienen: con alguno, no se elimina, solo se
/// desactiva.
/// </summary>
public record TipoEventoAdminDTO(int Id, string Nombre, bool RequiereEvidencia, NivelPrioridad NivelPrioridad, bool Activo,
    int Eventos);

/// <summary>Una opción de los filtros de Eventos, el Mapa y el Reporte: los activos y los inactivos que tienen eventos.</summary>
public record TipoEventoOpcionDTO(int Id, string Nombre, bool Activo);

public record GuardarTipoEventoDTO
{
    [Required(ErrorMessage = "Escribe el nombre del tipo de evento.")]
    [MaxLength(80, ErrorMessage = "El nombre no puede superar los 80 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    public bool RequiereEvidencia { get; init; }

    // Nullable para que [Required] detecte que falta: un enum sin valor tomaría el primero (Bajo) sin avisar.
    [Required(ErrorMessage = "Elige la prioridad.")]
    [EnumDataType(typeof(NivelPrioridad), ErrorMessage = "Elige una prioridad de la lista.")]
    public NivelPrioridad? NivelPrioridad { get; init; }
}
