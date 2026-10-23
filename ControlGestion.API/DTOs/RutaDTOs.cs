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

/// <summary>
/// Una ruta con sus visitas. Pendientes es lo que la app avisa antes de terminar (ext. 7.1a). UltimaUbicacion es la última
/// posición del promotor desde que empezó la ruta (paso 6.1); null si todavía no llegó ninguna (ext. 6.1a).
/// </summary>
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
    List<VisitaDTO> Visitas,
    UbicacionDTO? UltimaUbicacion);

public record UbicacionDTO(decimal Latitud, decimal Longitud, DateTime Fecha);

/// <summary>
/// Marcar una visita (7.3.20, pasos 5 a 5.2): visitado (1) o ausente (2), con la ubicación del celular. Sin ubicación no se
/// marca (ext. 5.1a). La hora la pone el servidor.
/// </summary>
public record MarcarVisitaDTO
{
    [Required(ErrorMessage = "Elige el resultado de la visita.")]
    [Range(1, 2, ErrorMessage = "El resultado es Visitado o Ausente.")]
    public int? Resultado { get; init; }

    [Required(ErrorMessage = "Sin ubicación no se marca la visita: activa el GPS.")]
    [Range(-90.0, 90.0, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
    public decimal? Latitud { get; init; }

    [Required(ErrorMessage = "Sin ubicación no se marca la visita: activa el GPS.")]
    [Range(-180.0, 180.0, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
    public decimal? Longitud { get; init; }

    [MaxLength(500, ErrorMessage = "La observación no puede superar los 500 caracteres.")]
    public string? Observacion { get; init; }
}

/// <summary>La posición del promotor que la app manda cada 30 segundos con la ruta en curso (paso 4.3).</summary>
public record RegistrarUbicacionDTO
{
    [Required(ErrorMessage = "Falta la latitud.")]
    [Range(-90.0, 90.0, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
    public decimal? Latitud { get; init; }

    [Required(ErrorMessage = "Falta la longitud.")]
    [Range(-180.0, 180.0, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
    public decimal? Longitud { get; init; }
}

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
