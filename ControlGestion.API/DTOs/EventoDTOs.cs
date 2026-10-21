using System.ComponentModel.DataAnnotations;
using ControlGestion.API.Models;

namespace ControlGestion.API.DTOs;

public record RegistrarEventoDTO
{
    public int TipoEventoId { get; init; }
    public int? ClienteId { get; init; }
    public int? PedidoId { get; init; }
    public decimal Latitud { get; init; }
    public decimal Longitud { get; init; }

    [Required(ErrorMessage = "Describe lo que pasó.")]
    public string Descripcion { get; init; } = string.Empty;
}

public record ResolverEventoDTO
{
    [Required(ErrorMessage = "Escribe cómo se resolvió.")]
    public string Observacion { get; init; } = string.Empty;
}

/// <summary>
/// Filtros de la lista de eventos (query string). Buscar compara con el número del evento, con el nombre y el código
/// del cliente y con la descripción. La zona es la del cliente. Desde y Hasta son días de Bolivia de la fecha del
/// evento, ambos incluidos.
/// </summary>
public record FiltrosEventoDTO : IValidatableObject
{
    [MaxLength(100, ErrorMessage = "La búsqueda no puede superar los 100 caracteres.")]
    public string? Buscar { get; init; }

    public int? TipoEventoId { get; init; }
    public EstadoEvento? Estado { get; init; }

    /// <summary>Quien registró el evento.</summary>
    public string? PromotorId { get; init; }

    public int? ZonaId { get; init; }
    public int? ClienteId { get; init; }
    public DateOnly? Desde { get; init; }
    public DateOnly? Hasta { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "La página empieza en 1.")]
    public int Pagina { get; init; } = 1;

    [Range(1, 100, ErrorMessage = "El tamaño de la página va de 1 a 100.")]
    public int Tamano { get; init; } = 25;

    public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
    {
        if (Desde > Hasta)
            yield return new ValidationResult("La fecha \"desde\" no puede ser posterior a \"hasta\".", [nameof(Desde)]);
    }
}

public record TipoEventoDTO(int Id, string Nombre, bool RequiereEvidencia, NivelPrioridad NivelPrioridad);

public record EventoListaDTO(
    int Id,
    string Numero,
    TipoEventoDTO Tipo,
    EstadoEvento Estado,
    DateTime FechaEvento,
    string Descripcion,
    ClienteResumenDTO? Cliente,
    PedidoResumenDTO? Pedido,
    /// <summary>Quien registró el evento.</summary>
    UsuarioResumenDTO Promotor,
    ZonaResumenDTO? Zona,
    int CantidadFotos);

public record FotoDTO(
    int Id,
    string NombreArchivo,
    DateTime FechaSubida,
    UsuarioResumenDTO SubidoPor,
    decimal? Latitud,
    decimal? Longitud,
    DateTime? FechaCaptura);

public record EventoDTO(
    int Id,
    string Numero,
    TipoEventoDTO Tipo,
    EstadoEvento Estado,
    DateTime FechaEvento,
    decimal Latitud,
    decimal Longitud,
    string Descripcion,
    ClienteResumenDTO? Cliente,
    PedidoResumenDTO? Pedido,
    UsuarioResumenDTO Promotor,
    ZonaResumenDTO? Zona,
    DateTime? FechaResolucion,
    UsuarioResumenDTO? ResueltoPor,
    IReadOnlyList<FotoDTO> Fotos);
