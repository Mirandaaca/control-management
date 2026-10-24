using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

/// <summary>Los filtros de la bitácora (7.3.35): usuario, módulo y días de Bolivia. Sin ninguno, todos los registros.</summary>
public record FiltrosBitacoraDTO : IValidatableObject
{
    public string? UsuarioId { get; init; }

    [MaxLength(60, ErrorMessage = "El módulo no puede superar los 60 caracteres.")]
    public string? Modulo { get; init; }

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

/// <summary>Un registro. Usuario es null en los de antes de la tarea 16 que se guardaron como "Anonimo".</summary>
public record RegistroBitacoraDTO(int Id, DateTime Fecha, UsuarioResumenDTO? Usuario, string Modulo, string Accion, string? DireccionIp);

/// <summary>Lo que piden los filtros: todos los usuarios (también los inactivos) y los 8 módulos.</summary>
public record OpcionesBitacoraDTO(List<UsuarioResumenDTO> Usuarios, IReadOnlyList<string> Modulos);
