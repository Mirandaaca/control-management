using System.ComponentModel.DataAnnotations;
using ControlGestion.API.Models;

namespace ControlGestion.API.DTOs;

/// <summary>
/// Planificar una ruta (7.3.20, pasos 2.1 a 2.4): la zona, el promotor, el día y los clientes en el orden de visita. Que
/// la fecha no haya pasado y que los clientes sean de la zona lo revisa RutaService.
/// </summary>
public record PlanificarRutaDTO
{
    [Required(ErrorMessage = "Elige la zona.")]
    [Range(1, int.MaxValue, ErrorMessage = "Elige la zona.")]
    public int? ZonaId { get; init; }

    [Required(ErrorMessage = "Elige el promotor.")]
    public string? PromotorId { get; init; }

    [Required(ErrorMessage = "Elige la fecha.")]
    public DateOnly? Fecha { get; init; }

    [Required(ErrorMessage = "Agrega al menos un cliente a la ruta.")]
    [MinLength(1, ErrorMessage = "Agrega al menos un cliente a la ruta.")]
    public List<int>? Clientes { get; init; }
}

/// <summary>La ruta sugerida se pide para una zona y un día (query string).</summary>
public record SugeridaDTO
{
    [Required(ErrorMessage = "Elige la zona.")]
    public int? ZonaId { get; init; }

    [Required(ErrorMessage = "Elige la fecha.")]
    public DateOnly? Fecha { get; init; }
}

/// <summary>Filtros de la lista de rutas (query string). El panel pide las de un día.</summary>
public record FiltrosRutaDTO
{
    public DateOnly? Fecha { get; init; }
    public string? PromotorId { get; init; }
    public int? ZonaId { get; init; }
    public EstadoRuta? Estado { get; init; }
}

/// <summary>Una ruta de la lista (7.3.20, paso 1). Marcadas son las visitadas y las ausentes: es el avance ("3 de 5").</summary>
public record RutaListaDTO(
    int Id,
    string Numero,
    DateOnly Fecha,
    EstadoRuta Estado,
    UsuarioResumenDTO Promotor,
    ZonaResumenDTO Zona,
    DateTime? FechaInicio,
    DateTime? FechaFin,
    int Total,
    int Marcadas,
    int Visitadas);

public record ClienteDeRutaDTO(
    int Id,
    string Codigo,
    string NombreCompleto,
    string DireccionTexto,
    decimal Latitud,
    decimal Longitud,
    DateTime? UltimaVisita,
    DateTime? ProximaVisita);

/// <summary>
/// Una visita de la ruta. La ubicación es la del celular al marcarla. DiasDeAtraso es el del cliente el día en que se
/// pide (null si nunca se visitó); solo tiene sentido mientras la visita está pendiente.
/// </summary>
public record VisitaDTO(
    int Id,
    int Orden,
    EstadoVisita Resultado,
    DateTime? HoraVisita,
    decimal? Latitud,
    decimal? Longitud,
    string? Observacion,
    ClienteDeRutaDTO Cliente,
    int? DiasDeAtraso);

public record RutaDTO(
    int Id,
    string Numero,
    DateOnly Fecha,
    EstadoRuta Estado,
    UsuarioResumenDTO Promotor,
    ZonaResumenDTO Zona,
    DateTime? FechaInicio,
    DateTime? FechaFin,
    int Total,
    int Marcadas,
    int Visitadas,
    int Pendientes,
    List<VisitaDTO> Visitas);

/// <summary>Un cliente de la ruta sugerida (7.3.20, paso 2.2), con su atraso al día de la ruta.</summary>
public record ClienteSugeridoDTO(
    int Id,
    string Codigo,
    string NombreCompleto,
    string DireccionTexto,
    decimal Latitud,
    decimal Longitud,
    DateTime? UltimaVisita,
    DateTime? ProximaVisita,
    int? DiasDeAtraso);
