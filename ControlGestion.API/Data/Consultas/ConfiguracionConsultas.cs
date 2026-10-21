using ControlGestion.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Data.Consultas;

public static class ConfiguracionConsultas
{
    /// <summary>
    /// Un parámetro en días (ClavesDeConfiguracion). La migración los siembra; si alguien borró la clave o le puso un
    /// valor que no es un entero mayor que cero, se usa el valor por defecto.
    /// </summary>
    public static async Task<int> DiasAsync(this DbSet<ConfiguracionGeneral> configuracion, string clave, int porDefecto)
    {
        var valor = await configuracion.Where(c => c.Clave == clave).Select(c => c.Valor).FirstOrDefaultAsync();
        return int.TryParse(valor, out var dias) && dias > 0 ? dias : porDefecto;
    }
}
