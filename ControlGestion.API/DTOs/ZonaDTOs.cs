using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

/// <summary>
/// Una zona de venta (7.2). PromotorValido: el promotor está activo y recorre rutas; si no, la zona se marca para que
/// el gerente general la reasigne. Clientes: los de la zona que el usuario puede ver (su cartera, o todos).
/// PuntosDeReferencia: cuántos tiene (se eliminan con ella). Limite: los vértices de su polígono (5.5), o null si
/// todavía no se dibujó.
/// </summary>
public record ZonaDTO(int Id, string Codigo, string Nombre, UsuarioResumenDTO? Promotor, bool PromotorValido, int Clientes,
    int PuntosDeReferencia, IReadOnlyList<VerticeDTO>? Limite);

/// <summary>Un vértice del límite de una zona.</summary>
public record VerticeDTO
{
    [Range(-90.0, 90.0, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
    public decimal Latitud { get; init; }

    [Range(-180.0, 180.0, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
    public decimal Longitud { get; init; }
}

/// <summary>El límite de una zona (5.5): un polígono de 3 puntos distintos o más. Uno nuevo reemplaza al anterior.</summary>
public record GuardarLimiteDTO : IValidatableObject
{
    [Required(ErrorMessage = "Dibuja el límite de la zona.")]
    [MinLength(3, ErrorMessage = "El límite necesita 3 puntos o más.")]
    [MaxLength(500, ErrorMessage = "El límite no puede tener más de 500 puntos.")]
    public List<VerticeDTO> Vertices { get; init; } = [];

    /// <summary>
    /// Los vértices redondeados a 6 decimales (unos 10 cm), como los guarda la base. Si el polígono llega cerrado (el
    /// último igual al primero), el último sobra: el mapa lo cierra solo.
    /// </summary>
    public List<VerticeDTO> Normalizados()
    {
        var redondeados = Vertices
            .Select(v => new VerticeDTO { Latitud = Math.Round(v.Latitud, 6), Longitud = Math.Round(v.Longitud, 6) })
            .ToList();
        if (redondeados.Count > 1 && redondeados[0] == redondeados[^1]) redondeados.RemoveAt(redondeados.Count - 1);
        return redondeados;
    }

    // Tres puntos repetidos no encierran nada: hacen falta 3 distintos (respuesta 2 de la tarea 12).
    public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
    {
        if (Vertices is { Count: >= 3 } && Normalizados().Distinct().Count() < 3)
            yield return new ValidationResult("Marca 3 puntos distintos o más.", [nameof(Vertices)]);
    }
}

/// <summary>Lo que se cambia de una zona: el código no se modifica (7.2).</summary>
public record ModificarZonaDTO
{
    [Required(ErrorMessage = "Escribe el nombre de la zona.")]
    [MaxLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Obligatorio: un usuario activo que recorre rutas (lo revisa el servicio).</summary>
    [Required(ErrorMessage = "Elige el promotor de la zona.")]
    public string PromotorId { get; init; } = string.Empty;
}

public record RegistrarZonaDTO : ModificarZonaDTO
{
    [Required(ErrorMessage = "Escribe el código de la zona.")]
    [MaxLength(20, ErrorMessage = "El código no puede superar los 20 caracteres.")]
    public string Codigo { get; init; } = string.Empty;
}
