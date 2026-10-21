using System.ComponentModel.DataAnnotations;
using ControlGestion.API.Models;

namespace ControlGestion.API.DTOs;

/// <summary>Lo que se puede cambiar de un evento (4.1): el tipo, el cliente o el pedido y la descripción.</summary>
public record ModificarEventoDTO : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Elige el tipo de evento.")]
    public int TipoEventoId { get; init; }

    /// <summary>Con el pedido, el cliente se puede omitir: es el del pedido.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Elige el cliente.")]
    public int? ClienteId { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Elige el pedido.")]
    public int? PedidoId { get; init; }

    [Required(ErrorMessage = "Describe lo que pasó.")]
    [MaxLength(1000, ErrorMessage = "La descripción no puede superar los 1000 caracteres.")]
    public string Descripcion { get; init; } = string.Empty;

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext contexto)
    {
        if (ClienteId is null && PedidoId is null)
            yield return new ValidationResult("Elige el cliente o el pedido del evento.", [nameof(ClienteId)]);
    }
}

public record RegistrarEventoDTO : ModificarEventoDTO
{
    // Sin ubicación no se guarda el evento: en la app la da el GPS sola, como en la HP.
    [Required(ErrorMessage = "Falta la ubicación del evento.")]
    [Range(-90.0, 90.0, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
    public decimal? Latitud { get; init; }

    [Required(ErrorMessage = "Falta la ubicación del evento.")]
    [Range(-180.0, 180.0, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
    public decimal? Longitud { get; init; }

    // 0,0 es lo que llega cuando nadie tomó la ubicación: un punto en el océano, no un evento real.
    public override IEnumerable<ValidationResult> Validate(ValidationContext contexto)
    {
        foreach (var error in base.Validate(contexto)) yield return error;
        if (Latitud == 0 && Longitud == 0)
            yield return new ValidationResult("Falta la ubicación del evento.", [nameof(Latitud)]);
    }
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
