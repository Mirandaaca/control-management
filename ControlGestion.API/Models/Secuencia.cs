using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.Models;

/// <summary>Último número entregado para cada clave de numeración ("HP-2026", "COB-2026", "CLI"…).</summary>
public class Secuencia
{
    [MaxLength(20)]
    public string Clave { get; set; } = string.Empty;

    public int Ultimo { get; set; }
}
