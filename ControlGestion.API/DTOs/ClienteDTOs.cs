using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

public record GuardarClienteDTO
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

    public decimal Latitud { get; init; }
    public decimal Longitud { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Elige la zona.")]
    public int ZonaId { get; init; }

    public string? PromotorId { get; init; }
    public int DiasRevisita { get; init; } = 15;
    public bool TieneCredito { get; init; }
    public decimal LimiteCredito { get; init; }
    public int EstadoClienteId { get; init; } = 1;
}

/// <summary>Filtros de la lista de clientes (query string). Buscar compara con nombre, código, celular, razón social y NIT.</summary>
public record FiltrosClienteDTO
{
    [MaxLength(100, ErrorMessage = "La búsqueda no puede superar los 100 caracteres.")]
    public string? Buscar { get; init; }

    public int? ZonaId { get; init; }
    public int? EstadoId { get; init; }
    public string? PromotorId { get; init; }

    /// <summary>Solo los clientes cuya próxima visita ya llegó (5.3).</summary>
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
