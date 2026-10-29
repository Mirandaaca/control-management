using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace ControlGestion.API.Services;

/// <summary>
/// Rutas de re-visita (5.4, 7.3.20): el gerente planifica y sigue; el promotor recorre la suya. Sin cartera completa, cada
/// usuario ve solo sus rutas.
/// </summary>
public class RutaService(AppDbContext db, IUsuarioActual usuario, NumeradorService numerador, RevisitaService revisitas,
    NavegacionService navegacion, TimeProvider reloj)
{
    private static readonly TimeSpan DiasDeUbicaciones = TimeSpan.FromDays(30);

    private IQueryable<Ruta> Visibles => db.Rutas.VisiblesPara(usuario);

    /// <summary>
    /// Borra las ubicaciones del promotor de hace más de 30 días: el seguimiento solo usa la última de una ruta en curso
    /// (7.3.20, paso 6.1), y son unas 3.000 filas al día con tres promotores. La llama el proceso diario.
    /// </summary>
    public Task<int> BorrarUbicacionesViejasAsync()
    {
        var antesDe = reloj.GetUtcNow().UtcDateTime - DiasDeUbicaciones;
        return db.UbicacionesPromotor.Where(u => u.Fecha < antesDe).ExecuteDeleteAsync();
    }

    public Task<List<RutaListaDTO>> ListarAsync(FiltrosRutaDTO filtros)
    {
        var consulta = Visibles;
        if (filtros.Fecha.HasValue) consulta = consulta.Where(r => r.FechaPlanificada == filtros.Fecha);
        if (!string.IsNullOrEmpty(filtros.PromotorId)) consulta = consulta.Where(r => r.PromotorId == filtros.PromotorId);
        if (filtros.ZonaId.HasValue) consulta = consulta.Where(r => r.ZonaId == filtros.ZonaId);
        if (filtros.Estado.HasValue) consulta = consulta.Where(r => r.Estado == filtros.Estado);
        return consulta
            .OrderByDescending(r => r.FechaPlanificada).ThenBy(r => r.Promotor!.NombreCompleto).ThenBy(r => r.Id)
            .Select(RutaConsultas.ComoLista)
            .ToListAsync();
    }

    public async Task<RutaDTO> ObtenerAsync(int id)
    {
        var ruta = await Visibles.Where(r => r.Id == id).Select(RutaConsultas.ComoDetalle).FirstOrDefaultAsync()
            ?? throw new NoEncontradoException("No se encontró la ruta.");
        return await CompletarAsync(ruta);
    }

    /// <summary>La ruta del día del promotor (7.3.20, pasos 3 y 3.1), para la app.</summary>
    public async Task<RutaDTO> DelDiaAsync()
    {
        var hoy = HoraBolivia.Hoy(reloj);
        var ruta = await db.Rutas.Where(r => r.PromotorId == usuario.Id && r.FechaPlanificada == hoy)
            .Select(RutaConsultas.ComoDetalle).FirstOrDefaultAsync()
            ?? throw new NoEncontradoException("No tienes una ruta para hoy.", "sin_ruta_del_dia");
        return await CompletarAsync(ruta);
    }

    /// <summary>
    /// Lo que la consulta no trae: los días de atraso de las visitas pendientes, contados el día en que se pide la ruta, y
    /// la última ubicación del promotor desde que la inició (y hasta que la terminó: una de la ruta de ayer no cuenta).
    /// </summary>
    private async Task<RutaDTO> CompletarAsync(RutaDTO ruta)
    {
        var hoy = HoraBolivia.Hoy(reloj);
        UbicacionDTO? ultima = null;
        if (ruta.FechaInicio is { } inicio)
        {
            var fin = ruta.FechaFin ?? DateTime.MaxValue;
            ultima = await db.UbicacionesPromotor
                .Where(u => u.PromotorId == ruta.Promotor.Id && u.Fecha >= inicio && u.Fecha <= fin)
                .OrderByDescending(u => u.Fecha).ThenByDescending(u => u.Id)
                .Select(u => new UbicacionDTO(u.Latitud, u.Longitud, u.Fecha))
                .FirstOrDefaultAsync();
        }
        return ruta with
        {
            Visitas = ruta.Visitas.Select(v => v with
            {
                DiasDeAtraso = v.Resultado == EstadoVisita.Pendiente ? ClienteConsultas.DiasDeAtraso(v.Cliente.ProximaVisita, hoy) : null
            }).ToList(),
            UltimaUbicacion = ultima,
        };
    }

    /// <summary>
    /// La ruta que el usuario recorre: visible y suya. Verla (cartera completa) no alcanza para recorrerla: lo que depende
    /// del dueño no es un permiso.
    /// </summary>
    private async Task<Ruta> SuyaAsync(int id)
    {
        var ruta = await Visibles.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NoEncontradoException("No se encontró la ruta.");
        if (ruta.PromotorId != usuario.Id)
            throw new ReglaDeNegocioException("ruta_de_otro_promotor", "Solo el promotor de la ruta puede recorrerla.");
        return ruta;
    }

    private static void ExigirEnCurso(Ruta ruta)
    {
        if (ruta.Estado != EstadoRuta.EnCurso)
            throw new ReglaDeNegocioException("ruta_no_en_curso", ruta.Estado == EstadoRuta.Planificada
                ? "Inicia la ruta antes de marcar las visitas."
                : "La ruta ya se completó.");
    }

    /// <summary>Iniciar (7.3.20, pasos 4.1 y 4.2): solo una ruta planificada (ext. 4.1a).</summary>
    public async Task<RutaDTO> IniciarAsync(int id)
    {
        var ruta = await SuyaAsync(id);
        if (ruta.Estado != EstadoRuta.Planificada)
            throw new ReglaDeNegocioException("ruta_ya_iniciada", ruta.Estado == EstadoRuta.EnCurso
                ? "La ruta ya está en curso."
                : "La ruta ya se completó.");
        ruta.Estado = EstadoRuta.EnCurso;
        ruta.FechaInicio = Ahora;
        await db.SaveChangesAsync();
        return await ObtenerAsync(id);
    }

    /// <summary>
    /// Marcar una visita (7.3.20, pasos 5.1 a 5.3). Visitado es una visita del cliente: lo pasa a activo y calcula su
    /// próxima visita, en la misma transacción. Ausente no cambia al cliente (ext. 5.3a) y se puede volver a marcar mientras
    /// la ruta sigue en curso, por ejemplo cuando el promotor vuelve en la tarde (decisión de la tarea 14). Un Visitado ya
    /// movió al cliente: no se vuelve a marcar.
    /// </summary>
    public async Task<RutaDTO> MarcarVisitaAsync(int id, int visitaId, MarcarVisitaDTO dto)
    {
        var ruta = await SuyaAsync(id);
        ExigirEnCurso(ruta);
        var visita = await db.RutasDetalle.Include(d => d.Cliente).FirstOrDefaultAsync(d => d.Id == visitaId && d.RutaId == id)
            ?? throw new NoEncontradoException("No se encontró la visita en la ruta.");
        if (visita.EstadoVisita == EstadoVisita.Visitado)
            throw new ReglaDeNegocioException("visita_ya_marcada", $"La visita a {visita.Cliente!.NombreCompleto} ya está marcada como visitada.");

        var ahora = Ahora;
        visita.EstadoVisita = (EstadoVisita)dto.Resultado!.Value;
        visita.HoraVisita = ahora;
        visita.Latitud = dto.Latitud;
        visita.Longitud = dto.Longitud;
        visita.Observaciones = string.IsNullOrWhiteSpace(dto.Observacion) ? null : dto.Observacion.Trim();
        if (visita.EstadoVisita == EstadoVisita.Visitado)
            await revisitas.RegistrarVisitaAsync(visita.Cliente!, ahora);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictoException("conflicto_concurrencia",
                "El cliente cambió mientras marcabas la visita. Vuelve a intentarlo.");
        }
        return await ObtenerAsync(id);
    }

    /// <summary>
    /// La posición del promotor (paso 4.3), con la hora del servidor. Solo con la ruta en curso: al terminarla, la API deja
    /// de recibirla (paso 7.1) y la app deja de enviarla.
    /// </summary>
    public async Task RegistrarUbicacionAsync(int id, RegistrarUbicacionDTO dto)
    {
        var ruta = await SuyaAsync(id);
        ExigirEnCurso(ruta);
        db.UbicacionesPromotor.Add(new UbicacionPromotor
        {
            PromotorId = ruta.PromotorId,
            Latitud = dto.Latitud!.Value,
            Longitud = dto.Longitud!.Value,
            Fecha = Ahora,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// El camino por calles desde donde está el promotor hasta el cliente de una visita (tarea 25). Solo el dueño y con la
    /// ruta en curso, como el resto del recorrido. El destino sale de la base: el celular solo manda dónde está.
    /// </summary>
    public async Task<CaminosDTO> CaminoAsync(int id, int visitaId, CaminoConsultaDTO consulta)
    {
        var ruta = await SuyaAsync(id);
        ExigirEnCurso(ruta);
        var destino = await db.RutasDetalle.Where(d => d.Id == visitaId && d.RutaId == id)
            .Select(d => new { d.Cliente!.Latitud, d.Cliente.Longitud })
            .FirstOrDefaultAsync()
            ?? throw new NoEncontradoException("No se encontró la visita en la ruta.");
        return await navegacion.CaminoAsync(consulta.Latitud!.Value, consulta.Longitud!.Value, destino.Latitud,
            destino.Longitud, consulta.Medio);
    }

    /// <summary>
    /// El orden más corto para lo que le falta al promotor, desde donde está (parte C de la tarea 25): las pendientes y
    /// las ausentes por las que vuelve a pasar. No guarda nada: el promotor lo ve y lo aplica con <see cref="ReordenarAsync"/>.
    /// </summary>
    public async Task<OrdenSugeridoDTO> OrdenSugeridoAsync(int id, OrdenConsultaDTO consulta)
    {
        var ruta = await SuyaAsync(id);
        ExigirEnCurso(ruta);
        var visitas = await db.RutasDetalle.Where(d => d.RutaId == id)
            .Select(d => new { d.Id, d.EstadoVisita, d.Cliente!.NombreCompleto, d.Cliente.Latitud, d.Cliente.Longitud })
            .ToListAsync();
        var ausentes = consulta.Ausentes.ToHashSet();
        if (ausentes.Any(a => !visitas.Any(v => v.Id == a && v.EstadoVisita == EstadoVisita.Ausente)))
            throw RutaCambio();
        var porOrdenar = visitas
            .Where(v => v.EstadoVisita == EstadoVisita.Pendiente || ausentes.Contains(v.Id))
            .ToList();
        if (porOrdenar.Count < 2)
            throw new ReglaDeNegocioException("nada_que_reordenar", "Para reordenar hacen falta al menos dos visitas por hacer.");

        var orden = await navegacion.OrdenAsync(consulta.Latitud!.Value, consulta.Longitud!.Value,
            porOrdenar.Select(v => (v.Latitud, v.Longitud)).ToList(), consulta.Medio);
        var hechas = visitas.Count - porOrdenar.Count;
        return new OrdenSugeridoDTO(
            orden.Indices.Select((i, n) => new VisitaOrdenadaDTO(porOrdenar[i].Id, hechas + n + 1,
                porOrdenar[i].NombreCompleto, porOrdenar[i].EstadoVisita == EstadoVisita.Ausente)).ToList(),
            orden.DuracionSegundos, orden.DistanciaMetros);
    }

    /// <summary>
    /// Aplica el orden que el promotor aceptó (parte C de la tarea 25). Primero van las visitas hechas, en el orden en que se
    /// marcaron, y después las del orden nuevo. Las ausentes por las que vuelve a pasar quedan pendientes otra vez (la
    /// bitácora guarda la marca que tenían): así la app las toma como la siguiente cuando les toca.
    /// </summary>
    public async Task<RutaDTO> ReordenarAsync(int id, ReordenarDTO dto)
    {
        var ruta = await SuyaAsync(id);
        ExigirEnCurso(ruta);
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            // Marcar una visita mientras tanto espera a que esto termine: no se cruzan.
            await db.Database.ExecuteSqlAsync($"SELECT Id FROM RutasDetalle WHERE RutaId = {id} FOR UPDATE");
            var detalles = await db.RutasDetalle.Where(d => d.RutaId == id).ToListAsync();
            var vuelven = dto.AusentesQueVuelven.ToHashSet();
            var porOrdenar = detalles
                .Where(d => d.EstadoVisita == EstadoVisita.Pendiente
                    || (d.EstadoVisita == EstadoVisita.Ausente && vuelven.Contains(d.Id)))
                .Select(d => d.Id)
                .ToHashSet();
            // Se marcó una visita después de calcular el orden, o la lista no es la de las visitas que faltan.
            var visitas = dto.Visitas!;
            if (vuelven.Any(v => !detalles.Any(d => d.Id == v && d.EstadoVisita == EstadoVisita.Ausente))
                || visitas.Count != visitas.Distinct().Count() || !porOrdenar.SetEquals(visitas))
                throw RutaCambio();

            var numero = 1;
            foreach (var hecha in detalles.Where(d => !porOrdenar.Contains(d.Id))
                         .OrderBy(d => d.HoraVisita).ThenBy(d => d.Orden))
                hecha.Orden = numero++;
            foreach (var visita in visitas.Select(v => detalles.Single(d => d.Id == v)))
            {
                if (visita.EstadoVisita == EstadoVisita.Ausente)
                {
                    visita.EstadoVisita = EstadoVisita.Pendiente;
                    visita.HoraVisita = null;
                    visita.Latitud = null;
                    visita.Longitud = null;
                    visita.Observaciones = null;
                }
                visita.Orden = numero++;
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        return await ObtenerAsync(id);
    }

    private static ConflictoException RutaCambio() =>
        new("ruta_cambio", "La ruta cambió mientras calculabas el orden: vuelve a calcularlo.");

    /// <summary>Terminar (pasos 7.1 y 7.2). Las visitas sin marcar quedan pendientes (ext. 7.1a): la app avisa cuántas antes.</summary>
    public async Task<RutaDTO> TerminarAsync(int id)
    {
        var ruta = await SuyaAsync(id);
        if (ruta.Estado != EstadoRuta.EnCurso)
            throw new ReglaDeNegocioException("ruta_no_en_curso", ruta.Estado == EstadoRuta.Planificada
                ? "La ruta todavía no se inició."
                : "La ruta ya se completó.");
        ruta.Estado = EstadoRuta.Completada;
        ruta.FechaFin = Ahora;
        await db.SaveChangesAsync();
        return await ObtenerAsync(id);
    }

    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;

    /// <summary>
    /// La ruta sugerida (7.3.20, pasos 2.1 y 2.2): los clientes de la zona con la re-visita vencida el día de la ruta, que es
    /// cuando se los visita. Primero el más atrasado (los nunca visitados van antes) y después, cada vez, el más cercano al
    /// anterior. Sin ubicación, un cliente no se puede ordenar ni dibujar: queda fuera.
    /// </summary>
    public async Task<List<ClienteSugeridoDTO>> SugeridaAsync(int zonaId, DateOnly fecha)
    {
        var candidatos = await db.Clientes.VisiblesPara(usuario)
            .Where(c => c.ZonaId == zonaId && c.Latitud != 0 && c.Longitud != 0)
            .PorVisitar(HoraBolivia.FinDelDiaUtc(fecha))
            .DelMasAtrasado()
            .Select(c => new ClienteSugeridoDTO(c.Id, c.Codigo, c.NombreCompleto, c.DireccionTexto, c.Latitud, c.Longitud,
                c.UltimaVisita, c.ProximaVisita, null))
            .ToListAsync();
        if (candidatos.Count == 0) return [];

        var orden = new List<ClienteSugeridoDTO> { candidatos[0] };
        var quedan = candidatos.Skip(1).ToList();
        while (quedan.Count > 0)
        {
            var anterior = orden[^1];
            var cercano = quedan.MinBy(c => Distancia(anterior.Latitud, anterior.Longitud, c.Latitud, c.Longitud))!;
            orden.Add(cercano);
            quedan.Remove(cercano);
        }
        return orden.Select(c => c with { DiasDeAtraso = ClienteConsultas.DiasDeAtraso(c.ProximaVisita, fecha) }).ToList();
    }

    /// <summary>La distancia en metros entre dos puntos (haversine): alcanza para ordenar las visitas de una zona.</summary>
    private static double Distancia(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        const double radioDeLaTierra = 6_371_000;
        static double Radianes(decimal grados) => (double)grados * Math.PI / 180;
        var dLat = Radianes(lat2 - lat1);
        var dLon = Radianes(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2)
            + Math.Cos(Radianes(lat1)) * Math.Cos(Radianes(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * radioDeLaTierra * Math.Asin(Math.Sqrt(a));
    }

    /// <summary>
    /// Planificar (7.3.20, pasos 2.3 y 2.4). La ruta queda asignada al promotor elegido, no a quien la planifica. Un
    /// promotor tiene una sola ruta por día (ext. 2.3b): lo revisa antes y, si dos se planifican a la vez, el índice único.
    /// </summary>
    public async Task<RutaDTO> PlanificarAsync(PlanificarRutaDTO dto)
    {
        var fecha = dto.Fecha!.Value;
        var errores = new Dictionary<string, string[]>();
        if (fecha < HoraBolivia.Hoy(reloj)) errores["fecha"] = ["La fecha ya pasó."];

        var zona = await db.Zonas.Where(z => z.Id == dto.ZonaId).Select(z => new { z.Id, z.Nombre }).FirstOrDefaultAsync();
        if (zona == null) errores["zonaId"] = ["Elige la zona."];

        var promotor = await db.Users.Where(u => u.Id == dto.PromotorId && u.Activo)
            .ConPermiso(db, Permisos.Territorial.RecorrerRuta)
            .Select(u => new { u.Id, u.NombreCompleto }).FirstOrDefaultAsync();
        if (promotor == null) errores["promotorId"] = ["Elige un promotor activo, que recorra rutas."];

        var problemaConLosClientes = zona == null ? null : await RevisarClientesAsync(dto.Clientes!, zona.Id, zona.Nombre);
        if (problemaConLosClientes != null) errores["clientes"] = [problemaConLosClientes];
        if (errores.Count > 0) throw new ValidacionException(errores);

        await ExigirDiaLibreAsync(promotor!.Id, promotor.NombreCompleto, fecha);

        // El número se pide con su propia sentencia: la transacción lo devuelve si la ruta no se guarda.
        await using var tx = await db.Database.BeginTransactionAsync();
        var ruta = new Ruta
        {
            Numero = await numerador.SiguienteDelAnioAsync(NumeradorService.Ruta),
            Nombre = $"{zona!.Nombre} · {FormatoBolivia.Fecha(fecha)}",
            PromotorId = promotor.Id,
            ZonaId = zona.Id,
            FechaPlanificada = fecha,
            Estado = EstadoRuta.Planificada,
        };
        for (var i = 0; i < dto.Clientes!.Count; i++)
            ruta.Detalles.Add(new RutaDetalle { ClienteId = dto.Clientes[i], Orden = i + 1 });
        db.Rutas.Add(ruta);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry })
        {
            await tx.RollbackAsync();
            db.ChangeTracker.Clear();
            await ExigirDiaLibreAsync(promotor.Id, promotor.NombreCompleto, fecha);
            throw;
        }
        await tx.CommitAsync();
        return await ObtenerAsync(ruta.Id);
    }

    /// <summary>
    /// Eliminar una ruta que todavía no se inició (extensión nueva de la 7.3.20, tarea 14): así se corrige una ruta, porque
    /// no se modifica; después se planifica otra para ese promotor y ese día. Sus visitas se eliminan con ella (cascada). Su
    /// número no se vuelve a usar.
    /// </summary>
    public async Task EliminarAsync(int id)
    {
        var ruta = await Visibles.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NoEncontradoException("No se encontró la ruta.");
        if (ruta.Estado != EstadoRuta.Planificada)
            throw new ReglaDeNegocioException("ruta_ya_iniciada", ruta.Estado == EstadoRuta.EnCurso
                ? "La ruta ya está en curso: no se puede eliminar."
                : "La ruta ya se completó: no se puede eliminar.");
        db.Rutas.Remove(ruta);
        await db.SaveChangesAsync();
    }

    /// <summary>Los clientes van sin repetirse, son de la zona y tienen ubicación (la precondición de la 7.3.20).</summary>
    private async Task<string?> RevisarClientesAsync(List<int> ids, int zonaId, string nombreDeLaZona)
    {
        if (ids.Distinct().Count() != ids.Count) return "Un cliente no puede ir dos veces en la ruta.";
        var clientes = await db.Clientes.VisiblesPara(usuario).Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.NombreCompleto, c.ZonaId, ConUbicacion = c.Latitud != 0 && c.Longitud != 0 })
            .ToListAsync();
        if (clientes.Count != ids.Count) return "Uno de los clientes no existe.";
        if (clientes.FirstOrDefault(c => c.ZonaId != zonaId) is { } deOtraZona)
            return $"{deOtraZona.NombreCompleto} no es de {nombreDeLaZona}.";
        if (clientes.FirstOrDefault(c => !c.ConUbicacion) is { } sinUbicacion)
            return $"{sinUbicacion.NombreCompleto} no tiene ubicación: regístrala antes de ponerlo en una ruta.";
        return null;
    }

    private async Task ExigirDiaLibreAsync(string promotorId, string nombre, DateOnly fecha)
    {
        var otra = await db.Rutas.Where(r => r.PromotorId == promotorId && r.FechaPlanificada == fecha)
            .Select(r => r.Numero).FirstOrDefaultAsync();
        if (otra != null)
            throw new ConflictoException("ruta_repetida", $"{nombre} ya tiene una ruta para el {FormatoBolivia.Fecha(fecha)} ({otra}).");
    }
}
