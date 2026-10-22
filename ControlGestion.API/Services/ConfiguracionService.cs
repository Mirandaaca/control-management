using System.Globalization;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Models;

namespace ControlGestion.API.Services;

/// <summary>
/// La configuración general (7.1, tesis 7.3.27). Por ahora solo se editan los días de re-visita: el plazo del crédito
/// (DiasVencimientoCredito) es fijo y no tiene pantalla. Las claves no se crean ni se borran desde la API.
/// </summary>
public class ConfiguracionService(AppDbContext db)
{
    public async Task<DiasRevisitaDTO> DiasRevisitaAsync() =>
        new(await db.ConfiguracionesGenerales.DiasAsync(ClavesDeConfiguracion.DiasRevisita, ClavesDeConfiguracion.DiasRevisitaPorDefecto));

    public async Task<DiasRevisitaDTO> CambiarDiasRevisitaAsync(int dias)
    {
        // La migración siembra la clave; si alguien la borró directo en la base, se vuelve a crear.
        var configuracion = await db.ConfiguracionesGenerales.FindAsync(ClavesDeConfiguracion.DiasRevisita);
        if (configuracion == null)
        {
            configuracion = new ConfiguracionGeneral
            {
                Clave = ClavesDeConfiguracion.DiasRevisita,
                Descripcion = "Días entre una visita al cliente y la siguiente (7.1)."
            };
            db.ConfiguracionesGenerales.Add(configuracion);
        }
        configuracion.Valor = dias.ToString(CultureInfo.InvariantCulture);
        await db.SaveChangesAsync();
        return new DiasRevisitaDTO(dias);
    }
}
