using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>
/// El ciclo de visitas del cliente (5.2): una visita lo pasa a Activo y calcula la próxima, y cada día los activos cuya
/// próxima visita llegó pasan a Disponible para re-visita. Los estados asignados a mano (Inactivo y otros) quedan fuera
/// del ciclo: reciben las fechas, para cuando vuelvan, pero no cambian de estado.
/// </summary>
public class RevisitaService(AppDbContext db, TimeProvider reloj)
{
    /// <summary>
    /// Una visita registrada: una HP confirmada o una visita marcada como Visitado en la ruta (7.3.18, ext. *a). La próxima
    /// se calcula con el parámetro general de días de re-visita (7.1). No guarda: lo hace quien llama, en su transacción.
    /// </summary>
    public async Task RegistrarVisitaAsync(Cliente cliente, DateTime ahora)
    {
        cliente.UltimaVisita = ahora;
        cliente.ProximaVisita = ahora.AddDays(await db.ConfiguracionesGenerales.DiasAsync(
            ClavesDeConfiguracion.DiasRevisita, ClavesDeConfiguracion.DiasRevisitaPorDefecto));
        if (await db.EstadosCliente.Where(e => e.Id == cliente.EstadoClienteId).Select(e => e.EsDelSistema).FirstAsync())
            cliente.EstadoClienteId = EstadosDeCliente.Activo;
    }

    /// <summary>
    /// El proceso diario (7.3.18, ext. *b): los clientes activos cuya próxima visita ya llegó pasan a disponible. Es un solo
    /// UPDATE con la misma regla que la lista por visitar, así correrlo otra vez (cada arranque de la API en Azure) no
    /// cambia nada.
    /// </summary>
    public Task<int> PasarADisponiblesAsync() =>
        db.Clientes
            .Where(c => c.EstadoClienteId == EstadosDeCliente.Activo)
            .Where(ClienteConsultas.ProximaVisitaLlego(HoraBolivia.FinDeHoyUtc(reloj)))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.EstadoClienteId, EstadosDeCliente.DisponibleParaRevisita));
}
