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
public class RutaService(AppDbContext db, IUsuarioActual usuario, NumeradorService numerador, TimeProvider reloj)
{
    private IQueryable<Ruta> Visibles => db.Rutas.VisiblesPara(usuario);

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
        return ConAtraso(ruta);
    }

    /// <summary>Los días de atraso de las visitas pendientes, contados el día en que se pide la ruta.</summary>
    private RutaDTO ConAtraso(RutaDTO ruta)
    {
        var hoy = HoraBolivia.Hoy(reloj);
        return ruta with
        {
            Visitas = ruta.Visitas.Select(v => v with
            {
                DiasDeAtraso = v.Resultado == EstadoVisita.Pendiente ? ClienteConsultas.DiasDeAtraso(v.Cliente.ProximaVisita, hoy) : null
            }).ToList()
        };
    }

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
