using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>
/// El seguimiento de las entregas (tarea 31, trabajo futuro): la app manda la ubicación del promotor mientras tiene
/// entregas en ruta o su ruta en curso, y el panel muestra a cada promotor con sus entregas de hoy, su última ubicación y
/// su rastro. Usa la misma tabla que el seguimiento de la ruta (7.3.20, paso 4.3).
/// </summary>
public class SeguimientoService(AppDbContext db, IUsuarioActual usuario, TimeProvider reloj)
{
    // El rastro del panel: por dónde pasó el promotor en la última media hora, con un punto cada 30 s como mucho. La app
    // manda uno cada 10 s: sin ralear, serían 180 puntos por promotor en cada actualización.
    private static readonly TimeSpan DuracionDelRastro = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan PasoDelRastro = TimeSpan.FromSeconds(30);

    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;

    /// <summary>Lo que se sigue del usuario: su ruta de hoy, si está en curso, y sus entregas en ruta de hoy.</summary>
    public async Task<SeguimientoDTO> DelUsuarioAsync()
    {
        var hoy = HoraBolivia.Hoy(reloj);
        var inicio = HoraBolivia.InicioDelDiaUtc(hoy);
        var fin = HoraBolivia.FinDelDiaUtc(hoy);
        var usuarioId = usuario.Id;
        var rutaEnCurso = await db.Rutas
            .Where(r => r.PromotorId == usuarioId && r.FechaPlanificada == hoy && r.Estado == EstadoRuta.EnCurso)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync();
        var entregas = await db.Pedidos.CountAsync(p => p.RepartidorId == usuarioId
            && p.EstadoPedidoId == EstadosDePedido.EnRuta
            && p.FechaEntregaEstimada >= inicio && p.FechaEntregaEstimada < fin);
        return new SeguimientoDTO(rutaEnCurso, entregas);
    }

    /// <summary>
    /// Guarda la ubicación del usuario, con la hora del servidor, si hay algo que seguir: su ruta en curso o una entrega en
    /// ruta. Si no, 409: la app deja de mandar (al registrar la última entrega, por ejemplo). Responde lo que se sigue, y
    /// con eso la app ajusta cada cuánto manda.
    /// </summary>
    public async Task<SeguimientoDTO> RegistrarAsync(RegistrarUbicacionDTO dto)
    {
        var seguimiento = await DelUsuarioAsync();
        if (seguimiento.RutaEnCurso is null && seguimiento.EntregasEnRuta == 0)
            throw new ConflictoException("sin_seguimiento",
                "No tienes una ruta en curso ni entregas en ruta: tu ubicación ya no se envía.");
        db.UbicacionesPromotor.Add(new UbicacionPromotor
        {
            PromotorId = usuario.Id,
            Latitud = dto.Latitud!.Value,
            Longitud = dto.Longitud!.Value,
            Fecha = Ahora,
        });
        await db.SaveChangesAsync();
        return seguimiento;
    }

    /// <summary>
    /// "Entregas de hoy" del panel: los pedidos asignados con entrega hoy (en Bolivia), menos los anulados, agrupados por
    /// promotor (por nombre), cada uno con sus entregas por hora, su última ubicación de hoy y su rastro. Sin cartera
    /// completa, solo los pedidos de la cartera del usuario.
    /// </summary>
    public async Task<EntregasDelDiaDTO> EntregasDelDiaAsync()
    {
        var ahora = Ahora;
        var hoy = HoraBolivia.Hoy(reloj);
        var inicio = HoraBolivia.InicioDelDiaUtc(hoy);
        var fin = HoraBolivia.FinDelDiaUtc(hoy);

        var pedidos = await db.Pedidos.VisiblesPara(usuario)
            .Where(p => p.RepartidorId != null && p.FechaEntregaEstimada >= inicio && p.FechaEntregaEstimada < fin
                && p.EstadoPedidoId != EstadosDePedido.Anulado)
            .OrderBy(p => p.Repartidor!.NombreCompleto).ThenBy(p => p.RepartidorId)
            .ThenBy(p => p.FechaEntregaEstimada).ThenBy(p => p.Id)
            .Select(p => new
            {
                Promotor = new UsuarioResumenDTO(p.Repartidor!.Id, p.Repartidor.NombreCompleto),
                Entrega = new EntregaDelDiaDTO(
                    p.Id,
                    p.Numero,
                    new EstadoPedidoDTO(p.EstadoPedido!.Id, p.EstadoPedido.Nombre),
                    p.FechaEntregaEstimada,
                    p.FechaEntregaReal,
                    p.DireccionEntrega,
                    new ClienteResumenDTO(p.Cliente!.Id, p.Cliente.NombreCompleto),
                    p.Cliente.Latitud != 0 && p.Cliente.Longitud != 0
                        ? new PuntoDTO(p.Cliente.Latitud, p.Cliente.Longitud)
                        : null),
            })
            .ToListAsync();
        if (pedidos.Count == 0) return new EntregasDelDiaDTO(hoy, ahora, []);

        var ids = pedidos.Select(p => p.Promotor.Id).Distinct().ToList();
        var ultimas = await UltimasDeHoyAsync(ids, inicio);
        var desde = ahora - DuracionDelRastro > inicio ? ahora - DuracionDelRastro : inicio;
        var recientes = (await db.UbicacionesPromotor
                .Where(u => ids.Contains(u.PromotorId) && u.Fecha >= desde)
                .OrderBy(u => u.Fecha).ThenBy(u => u.Id)
                .Select(u => new { u.PromotorId, u.Latitud, u.Longitud, u.Fecha })
                .ToListAsync())
            .ToLookup(u => u.PromotorId, u => new UbicacionDTO(u.Latitud, u.Longitud, u.Fecha));

        // GroupBy conserva el orden de la consulta: los promotores por nombre y sus entregas por hora.
        var promotores = pedidos
            .GroupBy(p => p.Promotor.Id)
            .Select(g =>
            {
                var entregas = g.Select(p => p.Entrega).ToList();
                return new RepartidorDelDiaDTO(
                    g.First().Promotor,
                    entregas.Count,
                    entregas.Count(e => e.Estado.Id == EstadosDePedido.Entregado),
                    entregas.Count(e => e.Estado.Id == EstadosDePedido.EnRuta),
                    ultimas.GetValueOrDefault(g.Key),
                    Ralear(recientes[g.Key]),
                    entregas);
            })
            .ToList();
        return new EntregasDelDiaDTO(hoy, ahora, promotores);
    }

    /// <summary>
    /// La última ubicación de hoy de cada promotor, aunque tenga horas (si la app dejó de mandar, el panel lo muestra con su
    /// hora). Primero la hora más nueva de cada uno y después sus filas: así se usa el índice (PromotorId, Fecha).
    /// </summary>
    private async Task<Dictionary<string, UbicacionDTO>> UltimasDeHoyAsync(List<string> ids, DateTime inicio)
    {
        var maximas = await db.UbicacionesPromotor
            .Where(u => ids.Contains(u.PromotorId) && u.Fecha >= inicio)
            .GroupBy(u => u.PromotorId)
            .Select(g => new { PromotorId = g.Key, Fecha = g.Max(u => u.Fecha) })
            .ToListAsync();
        if (maximas.Count == 0) return new Dictionary<string, UbicacionDTO>();
        var fechas = maximas.Select(m => m.Fecha).Distinct().ToList();
        var filas = await db.UbicacionesPromotor
            .Where(u => ids.Contains(u.PromotorId) && fechas.Contains(u.Fecha))
            .Select(u => new { u.Id, u.PromotorId, u.Latitud, u.Longitud, u.Fecha })
            .ToListAsync();
        // Otro promotor puede tener una fila con la misma hora: cada uno se queda con la suya, y con la de id mayor si
        // llegaron dos en el mismo instante.
        return maximas.ToDictionary(m => m.PromotorId, m =>
        {
            var fila = filas.Where(f => f.PromotorId == m.PromotorId && f.Fecha == m.Fecha).MaxBy(f => f.Id)!;
            return new UbicacionDTO(fila.Latitud, fila.Longitud, fila.Fecha);
        });
    }

    /// <summary>
    /// Deja un punto cada 30 s como mucho, en orden, y siempre el último: la línea del rastro termina donde está el promotor.
    /// </summary>
    private static List<UbicacionDTO> Ralear(IEnumerable<UbicacionDTO> puntos)
    {
        var rastro = new List<UbicacionDTO>();
        UbicacionDTO? ultimo = null;
        foreach (var punto in puntos)
        {
            ultimo = punto;
            if (rastro.Count == 0 || punto.Fecha - rastro[^1].Fecha >= PasoDelRastro) rastro.Add(punto);
        }
        if (ultimo is not null && !ReferenceEquals(rastro[^1], ultimo)) rastro.Add(ultimo);
        return rastro;
    }
}
