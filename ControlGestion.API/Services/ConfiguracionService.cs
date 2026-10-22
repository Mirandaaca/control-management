using System.Globalization;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>
/// La configuración general (7.1, tesis 7.3.27). Por ahora solo se editan los días de re-visita: el plazo del crédito
/// (DiasVencimientoCredito) es fijo y no tiene pantalla. Las claves no se crean ni se borran desde la API.
/// </summary>
public class ConfiguracionService(AppDbContext db, TimeProvider reloj)
{
    public async Task<DiasRevisitaDTO> DiasRevisitaAsync() =>
        new(await db.ConfiguracionesGenerales.DiasAsync(ClavesDeConfiguracion.DiasRevisita, ClavesDeConfiguracion.DiasRevisitaPorDefecto));

    /// <summary>
    /// Cambia los días y, en la misma transacción, mueve la próxima visita de todos los clientes con una visita
    /// registrada (última visita + días nuevos). Los que siguen el ciclo (Activo o Disponible) cambian de estado en el
    /// momento, en las dos direcciones (5.2): si la visita nueva ya llegó, Disponible; si no, Activo. Inactivo y los demás
    /// estados asignados a mano reciben la fecha, para cuando vuelvan al ciclo, pero conservan su estado.
    /// </summary>
    public async Task<DiasRevisitaCambiadosDTO> CambiarDiasRevisitaAsync(int dias)
    {
        var finDeHoy = HoraBolivia.FinDeHoyUtc(reloj);
        await using var tx = await db.Database.BeginTransactionAsync();

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

        // Sentencias directas en la base: cambian VersionConcurrencia de cada cliente, así una HP que se confirme al mismo
        // tiempo recibe un conflicto en vez de pisar la fecha con los días viejos.
        var visitados = db.Clientes.Where(c => c.UltimaVisita != null);
        var recalculados = await visitados.ExecuteUpdateAsync(s =>
            s.SetProperty(c => c.ProximaVisita, c => c.UltimaVisita!.Value.AddDays(dias)));
        var pasaronADisponible = await visitados
            .Where(c => c.EstadoClienteId == EstadosDeCliente.Activo)
            .Where(ClienteConsultas.ProximaVisitaLlego(finDeHoy))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.EstadoClienteId, EstadosDeCliente.DisponibleParaRevisita));
        var pasaronAActivo = await visitados
            .Where(c => c.EstadoClienteId == EstadosDeCliente.DisponibleParaRevisita)
            .Where(ClienteConsultas.ProximaVisitaPendiente(finDeHoy))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.EstadoClienteId, EstadosDeCliente.Activo));

        await tx.CommitAsync();
        return new DiasRevisitaCambiadosDTO(dias, recalculados, pasaronADisponible, pasaronAActivo);
    }
}
