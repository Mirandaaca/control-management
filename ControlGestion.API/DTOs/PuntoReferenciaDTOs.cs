using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

public record PuntoReferenciaDTO(int Id, ZonaResumenDTO Zona, string Nombre, string? Descripcion, decimal Latitud, decimal Longitud);

public record GuardarPuntoReferenciaDTO : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Elige la zona.")]
    public int ZonaId { get; init; }

    [Required(ErrorMessage = "Escribe el nombre del punto.")]
    [MaxLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    [MaxLength(300, ErrorMessage = "La descripción no puede superar los 300 caracteres.")]
    public string? Descripcion { get; init; }

    [Required(ErrorMessage = "Marca el punto en el mapa.")]
    [Range(-90.0, 90.0, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
    public decimal? Latitud { get; init; }

    [Required(ErrorMessage = "Marca el punto en el mapa.")]
    [Range(-180.0, 180.0, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
    public decimal? Longitud { get; init; }

    // 0,0 es lo que llega cuando nadie marcó el punto: un lugar en el océano, como en los clientes.
    public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
    {
        if (Latitud == 0 && Longitud == 0)
            yield return new ValidationResult("Marca el punto en el mapa.", [nameof(Latitud)]);
    }
}
