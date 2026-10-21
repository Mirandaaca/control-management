namespace ControlGestion.API.Tests.Infraestructura;

/// <summary>Un reloj que arranca en la hora real y se adelanta a mano: para probar lo que vence con el tiempo, como una sesión.</summary>
public class RelojAjustable : TimeProvider
{
    private DateTimeOffset _ahora = System.GetUtcNow();

    public override DateTimeOffset GetUtcNow() => _ahora;

    public void Adelantar(TimeSpan tiempo) => _ahora += tiempo;
}
