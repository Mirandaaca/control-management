using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

public record GuardarClienteDTO : IValidatableObject
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string NombreCompleto { get; init; } = string.Empty;

    [MaxLength(150, ErrorMessage = "La razón social no puede superar los 150 caracteres.")]
    public string? RazonSocial { get; init; }

    [MaxLength(20, ErrorMessage = "El NIT no puede superar los 20 caracteres.")]
    public string? NIT { get; init; }

    [Required(ErrorMessage = "El celular es obligatorio.")]
    [MaxLength(20, ErrorMessage = "El celular no puede superar los 20 caracteres.")]
    public string Telefono { get; init; } = string.Empty;

    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    [MaxLength(256, ErrorMessage = "El correo no puede superar los 256 caracteres.")]
    public string? Email { get; init; }

    [Required(ErrorMessage = "La dirección es obligatoria.")]
    [MaxLength(300, ErrorMessage = "La dirección no puede superar los 300 caracteres.")]
    public string DireccionTexto { get; init; } = string.Empty;

    // Sin ubicación no se guarda el cliente: en la app la da el GPS y en el panel se marca en el mapa.
    [Required(ErrorMessage = "Marca la ubicación del cliente en el mapa.")]
    [Range(-90.0, 90.0, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
    public decimal? Latitud { get; init; }

    [Required(ErrorMessage = "Marca la ubicación del cliente en el mapa.")]
    [Range(-180.0, 180.0, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
    public decimal? Longitud { get; init; }

    // El promotor del cliente no se elige: es el de su zona (7.2).
    [Range(1, int.MaxValue, ErrorMessage = "Elige la zona.")]
    public int ZonaId { get; init; }

    public int DiasRevisita { get; init; } = 15;
    public bool TieneCredito { get; init; }
    public decimal LimiteCredito { get; init; }

    // 0,0 es lo que guardaba el formulario viejo con los campos vacíos: un punto en el océano, no una ubicación real.
    public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
    {
        if (Latitud == 0 && Longitud == 0)
            yield return new ValidationResult("Marca la ubicación del cliente en el mapa.", [nameof(Latitud)]);
    }
}

/// <summary>
/// Un estado asignado a mano (Inactivo y otros) saca al cliente del ciclo de visitas. Null lo devuelve al ciclo: el
/// sistema vuelve a decidir entre Activo y Disponible para re-visita según su próxima visita.
/// </summary>
public record AsignarEstadoClienteDTO(int? EstadoClienteId);

/// <summary>Filtros de la lista de clientes (query string). Buscar compara con nombre, código, celular, razón social y NIT.</summary>
public record FiltrosClienteDTO
{
    [MaxLength(100, ErrorMessage = "La búsqueda no puede superar los 100 caracteres.")]
    public string? Buscar { get; init; }

    public int? ZonaId { get; init; }
    public int? EstadoId { get; init; }
    public string? PromotorId { get; init; }

    /// <summary>Solo los clientes del ciclo de visitas cuya próxima visita ya llegó o que nunca se visitaron (5.3).</summary>
    public bool PorVisitar { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "La página empieza en 1.")]
    public int Pagina { get; init; } = 1;

    [Range(1, 100, ErrorMessage = "El tamaño de la página va de 1 a 100.")]
    public int Tamano { get; init; } = 25;
}

public record EstadoClienteDTO(int Id, string Nombre, string Color, string? Descripcion);

public record ClienteListaDTO(
    int Id,
    string Codigo,
    string NombreCompleto,
    string Telefono,
    ZonaResumenDTO Zona,
    EstadoClienteDTO Estado,
    UsuarioResumenDTO? Promotor,
    decimal SaldoPendiente,
    DateTime? UltimaVisita,
    DateTime? ProximaVisita);

public record ClienteDTO(
    int Id,
    string Codigo,
    string NombreCompleto,
    string? RazonSocial,
    string? NIT,
    string Telefono,
    string? Email,
    string DireccionTexto,
    decimal Latitud,
    decimal Longitud,
    ZonaResumenDTO Zona,
    EstadoClienteDTO Estado,
    UsuarioResumenDTO? Promotor,
    decimal SaldoPendiente,
    DateTime? UltimaVisita,
    DateTime? ProximaVisita,
    DateTime FechaCreacion);

/// <summary>Lo justo para elegir un cliente en un formulario (HP, pedido, evento, ruta).</summary>
public record ClienteOpcionDTO(int Id, string Codigo, string NombreCompleto, int ZonaId);

public record ClienteMapaDTO(
    int Id,
    string Codigo,
    string NombreCompleto,
    decimal Latitud,
    decimal Longitud,
    string Telefono,
    string DireccionTexto,
    DateTime? ProximaVisita,
    EstadoClienteDTO Estado);
