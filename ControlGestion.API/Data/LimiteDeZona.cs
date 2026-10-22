using System.Text.Json;
using ControlGestion.API.DTOs;

namespace ControlGestion.API.Data;

/// <summary>
/// El límite de una zona se guarda como JSON en Zonas.Limite (decisión de la tarea 12): pocas zonas y ninguna consulta
/// geométrica en la base. La forma es la misma que la del DTO: [{"latitud": -17.78, "longitud": -63.18}, …].
/// </summary>
public static class LimiteDeZona
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    public static string Escribir(IReadOnlyList<VerticeDTO> vertices) => JsonSerializer.Serialize(vertices, Opciones);

    public static IReadOnlyList<VerticeDTO>? Leer(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<List<VerticeDTO>>(json, Opciones);
}
