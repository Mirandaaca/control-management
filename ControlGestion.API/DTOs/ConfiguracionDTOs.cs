using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

/// <summary>Los días entre una visita al cliente y la siguiente (7.1). Uno solo para todos los clientes.</summary>
public record DiasRevisitaDTO(int Dias);

/// <summary>
/// Lo que pasó al cambiar los días: a cuántos clientes con una visita registrada se les recalculó la próxima visita, y
/// cuántos de los que siguen el ciclo cambiaron de estado en cada dirección.
/// </summary>
public record DiasRevisitaCambiadosDTO(int Dias, int ClientesRecalculados, int PasaronADisponible, int PasaronAActivo);

public record CambiarDiasRevisitaDTO
{
    // El máximo evita fechas absurdas por un error de tipeo: con más de un año, el cliente sale del trabajo de campo.
    [Range(1, 365, ErrorMessage = "Los días de re-visita van de 1 a 365.")]
    public int Dias { get; init; }
}
