using ControlGestion.API.Configuracion;
using Microsoft.Extensions.Options;

namespace ControlGestion.API.Services;

/// <summary>
/// Cuenta los pedidos al proveedor de caminos en el día de Bolivia, entre todos los usuarios, para no pasar el plan
/// gratuito (tarea 25). Vive en memoria: si la API se reinicia vuelve a cero, y entonces protege el tope del propio
/// proveedor, que responde 429 (y la API, 503).
/// </summary>
public class CupoDeNavegacion(TimeProvider reloj, IOptions<NavegacionOptions> opciones)
{
    private readonly Lock _candado = new();
    private DateOnly _dia;
    private int _usados;

    /// <summary>Anota un pedido si todavía queda cupo hoy.</summary>
    public bool Consumir()
    {
        var hoy = HoraBolivia.Hoy(reloj);
        lock (_candado)
        {
            if (hoy != _dia)
            {
                _dia = hoy;
                _usados = 0;
            }
            if (_usados >= opciones.Value.TopeDiario) return false;
            _usados++;
            return true;
        }
    }
}
