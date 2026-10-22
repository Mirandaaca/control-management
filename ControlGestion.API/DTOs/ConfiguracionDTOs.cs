using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

/// <summary>Los días entre una visita al cliente y la siguiente (7.1). Uno solo para todos los clientes.</summary>
public record DiasRevisitaDTO(int Dias);

public record CambiarDiasRevisitaDTO
{
    // El máximo evita fechas absurdas por un error de tipeo: con más de un año, el cliente sale del trabajo de campo.
    [Range(1, 365, ErrorMessage = "Los días de re-visita van de 1 a 365.")]
    public int Dias { get; init; }
}
