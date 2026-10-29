using ControlGestion.API.Configuracion;
using Microsoft.Extensions.Options;

namespace ControlGestion.API.Services;

/// <summary>Lo que se le pide al proveedor de rutas: cada uno tiene su propio cupo en el plan.</summary>
public enum PedidoAlProveedor
{
    Camino,
    Orden
}

/// <summary>
/// Cuenta los pedidos al proveedor de caminos en el día de Bolivia, entre todos los usuarios, para no pasar el plan
/// gratuito (tarea 25): los caminos y los órdenes, cada uno con su tope. Vive en memoria: si la API se reinicia vuelve a
/// cero, y entonces protege el tope del propio proveedor, que responde 429 (y la API, 503).
/// </summary>
public class CupoDeNavegacion(TimeProvider reloj, IOptions<NavegacionOptions> opciones)
{
    private readonly Lock _candado = new();
    private DateOnly _dia;
    private readonly Dictionary<PedidoAlProveedor, int> _usados = [];

    /// <summary>Anota un pedido si todavía queda cupo hoy.</summary>
    public bool Consumir(PedidoAlProveedor pedido)
    {
        var tope = pedido == PedidoAlProveedor.Orden ? opciones.Value.TopeDiarioDeOrdenes : opciones.Value.TopeDiario;
        var hoy = HoraBolivia.Hoy(reloj);
        lock (_candado)
        {
            if (hoy != _dia)
            {
                _dia = hoy;
                _usados.Clear();
            }
            var usados = _usados.GetValueOrDefault(pedido);
            if (usados >= tope) return false;
            _usados[pedido] = usados + 1;
            return true;
        }
    }
}
