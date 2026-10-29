using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

/// <summary>Cómo va el promotor ese día. OpenRouteService no tiene perfil de moto: la moto va como auto.</summary>
public enum MedioDeTransporte
{
    Auto = 0,
    Moto = 1,
    Pie = 2
}

/// <summary>El giro de cada paso del camino. Es de la API, no del proveedor: la app elige el ícono con él.</summary>
public enum Giro
{
    Salida = 0,
    Recto = 1,
    Izquierda = 2,
    Derecha = 3,
    IzquierdaCerrada = 4,
    DerechaCerrada = 5,
    IzquierdaLeve = 6,
    DerechaLeve = 7,
    MantenerIzquierda = 8,
    MantenerDerecha = 9,
    Rotonda = 10,
    SalirDeRotonda = 11,
    Retorno = 12,
    Llegada = 13
}

/// <summary>
/// Desde dónde pide el camino el promotor y cómo va (query string, tarea 25). El destino no se pide: es el cliente de la
/// visita, que la API lee de la base.
/// </summary>
public record CaminoConsultaDTO : IValidatableObject
{
    [Required(ErrorMessage = "Sin ubicación no se calcula el camino: activa el GPS.")]
    [Range(-90.0, 90.0, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
    public decimal? Latitud { get; init; }

    [Required(ErrorMessage = "Sin ubicación no se calcula el camino: activa el GPS.")]
    [Range(-180.0, 180.0, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
    public decimal? Longitud { get; init; }

    [EnumDataType(typeof(MedioDeTransporte), ErrorMessage = "El medio debe ser auto, moto o pie.")]
    public MedioDeTransporte Medio { get; init; } = MedioDeTransporte.Auto;

    // 0,0 es un punto en el océano: el GPS todavía no dio una ubicación.
    public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
    {
        if (Latitud == 0 && Longitud == 0)
            yield return new ValidationResult("Sin ubicación no se calcula el camino: activa el GPS.", [nameof(Latitud)]);
    }
}

public record PuntoDTO(decimal Latitud, decimal Longitud);

/// <summary>
/// Un tramo del camino: la indicación para empezarlo ("Gira a la derecha en Calle 4"), lo que mide y dónde empieza.
/// <see cref="Indice"/> es la posición de <see cref="Inicio"/> en los puntos del camino: la app pasa a este paso cuando el
/// promotor llega a ese punto.
/// </summary>
public record PasoDTO(
    string Indicacion,
    string? Calle,
    Giro Giro,
    int DistanciaMetros,
    int DuracionSegundos,
    PuntoDTO Inicio,
    int Indice);

public record CaminoDTO(int DistanciaMetros, int DuracionSegundos, List<PuntoDTO> Puntos, List<PasoDTO> Pasos);

/// <summary>El mejor camino y hasta dos alternativas, que la app dibuja en gris para que el promotor elija.</summary>
public record CaminosDTO(CaminoDTO Principal, List<CaminoDTO> Alternativas);

/// <summary>
/// Desde dónde pide el orden el promotor, cómo va y por cuáles visitas ausentes vuelve a pasar (query string, parte C de
/// la tarea 25). Las pendientes entran siempre.
/// </summary>
public record OrdenConsultaDTO : CaminoConsultaDTO
{
    public List<int> Ausentes { get; init; } = [];
}

/// <summary>Una visita en el orden propuesto, con el número que tendría. <see cref="Ausente"/>: vuelve a pasar por ella.</summary>
public record VisitaOrdenadaDTO(int Id, int Orden, string NombreCompleto, bool Ausente);

/// <summary>El orden más corto para lo que falta, con lo que tarda y mide el recorrido. No se guarda hasta aplicarlo.</summary>
public record OrdenSugeridoDTO(List<VisitaOrdenadaDTO> Visitas, int DuracionSegundos, int DistanciaMetros);

/// <summary>El orden que el promotor aceptó: las visitas que faltan en el orden nuevo y las ausentes que vuelven.</summary>
public record ReordenarDTO
{
    [Required(ErrorMessage = "Falta el orden de las visitas.")]
    [MinLength(2, ErrorMessage = "Reordenar necesita al menos dos visitas.")]
    public List<int>? Visitas { get; init; }

    public List<int> AusentesQueVuelven { get; init; } = [];
}
