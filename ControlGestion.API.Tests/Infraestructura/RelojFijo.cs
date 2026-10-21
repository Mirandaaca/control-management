namespace ControlGestion.API.Tests.Infraestructura;

/// <summary>Un reloj parado en un momento: para probar lo que depende de la fecha (numeración por año, vencimientos).</summary>
public class RelojFijo(DateTimeOffset ahora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => ahora;
}
