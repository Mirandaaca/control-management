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
/// Eventos e incidencias (4.1 a 4.4). Cada usuario ve los que registró, los de su cartera y los de los pedidos que tiene
/// asignados, salvo con cartera completa.
/// </summary>
public class EventoService(AppDbContext db, IUsuarioActual usuario, NumeradorService numerador, EvidenciaService evidencias,
    TimeProvider reloj)
{
    public const int MaximoDeFotos = 5;
    public const int MaximoDePuntos = 1000;

    private IQueryable<Evento> Visibles => db.Eventos.VisiblesPara(usuario);

    public async Task<Pagina<EventoListaDTO>> ListarAsync(FiltrosEventoDTO filtros)
    {
        var consulta = Visibles.Filtrar(filtros);
        var total = await consulta.CountAsync();
        var items = await consulta
            .OrderByDescending(e => e.FechaEvento).ThenByDescending(e => e.Id)
            .Skip((filtros.Pagina - 1) * filtros.Tamano)
            .Take(filtros.Tamano)
            .Select(EventoConsultas.ComoLista)
            .ToListAsync();
        return new Pagina<EventoListaDTO>(items, total, filtros.Pagina, filtros.Tamano);
    }

    public async Task<EventoDTO> ObtenerAsync(int id) =>
        await Visibles.Where(e => e.Id == id).Select(EventoConsultas.ComoDetalle).FirstOrDefaultAsync()
        ?? throw new NoEncontradoException("No se encontró el evento.");

    /// <summary>
    /// El mapa de eventos (4.3), con los filtros de la lista: los más recientes, hasta 1.000, y cuántos cumplen los
    /// filtros, para avisar cuando el mapa no los muestra todos.
    /// </summary>
    public async Task<MapaEventosDTO> MapaAsync(FiltrosEventoDTO filtros)
    {
        var consulta = Visibles.Filtrar(filtros);
        var total = await consulta.CountAsync();
        var puntos = await consulta
            .OrderByDescending(e => e.FechaEvento).ThenByDescending(e => e.Id)
            .Take(MaximoDePuntos)
            .Select(EventoConsultas.ComoPunto)
            .ToListAsync();
        return new MapaEventosDTO(puntos, total);
    }

    public Task<List<TipoEventoDTO>> TiposAsync() =>
        db.TiposEvento.Where(t => t.Activo).OrderBy(t => t.Id).Select(EventoConsultas.TipoComoDTO).ToListAsync();

    /// <summary>
    /// Registrar (4.1): queda Pendiente y a nombre de quien lo registra, con la ubicación del momento. El evento siempre
    /// guarda a su cliente: si viene el pedido, es el del pedido.
    /// </summary>
    public async Task<EventoDTO> RegistrarAsync(RegistrarEventoDTO dto)
    {
        await ExigirTipoActivoAsync(dto.TipoEventoId);
        var (clienteId, pedidoId) = await VinculoAsync(dto);

        var evento = new Evento
        {
            TipoEventoId = dto.TipoEventoId,
            ClienteId = clienteId,
            PedidoId = pedidoId,
            PromotorId = usuario.Id,
            FechaEvento = Ahora,
            Latitud = dto.Latitud!.Value,
            Longitud = dto.Longitud!.Value,
            Descripcion = dto.Descripcion.Trim(),
            EstadoEvento = EstadoEvento.Pendiente
        };
        // El número se pide después de validar: un evento rechazado no gasta un número.
        evento.Numero = await numerador.SiguienteDelAnioAsync(NumeradorService.Evento);

        db.Eventos.Add(evento);
        await db.SaveChangesAsync();
        return await ObtenerAsync(evento.Id);
    }

    private async Task ExigirTipoActivoAsync(int tipoEventoId)
    {
        if (!await db.TiposEvento.AnyAsync(t => t.Id == tipoEventoId && t.Activo))
            throw new ReglaDeNegocioException("tipo_no_valido", "Elige un tipo de evento de la lista.");
    }

    /// <summary>
    /// El cliente y el pedido del evento, los dos visibles para quien registra. Uno ajeno responde lo mismo que uno que no
    /// existe, como en la HP. Un cliente inactivo se acepta: el evento puede explicar justo por qué quedó inactivo.
    /// </summary>
    private async Task<(int ClienteId, int? PedidoId)> VinculoAsync(ModificarEventoDTO dto)
    {
        if (dto.PedidoId is { } pedidoId)
        {
            var pedido = await db.Pedidos.VisiblesPara(usuario)
                .Where(p => p.Id == pedidoId)
                .Select(p => new { p.Id, p.Numero, p.ClienteId })
                .FirstOrDefaultAsync()
                ?? throw new ReglaDeNegocioException("pedido_no_existe", "No se encontró el pedido.");
            if (dto.ClienteId is { } elegido && elegido != pedido.ClienteId)
                throw new ReglaDeNegocioException("cliente_no_coincide",
                    $"El pedido {pedido.Numero} es de otro cliente: elige el cliente del pedido.");
            return (pedido.ClienteId, pedido.Id);
        }

        var clienteId = dto.ClienteId!.Value;
        if (!await db.Clientes.VisiblesPara(usuario).AnyAsync(c => c.Id == clienteId))
            throw new ReglaDeNegocioException("cliente_no_existe", "No se encontró el cliente.");
        return (clienteId, null);
    }

    /// <summary>
    /// Modificar (4.1): el tipo, el cliente o el pedido y la descripción, mientras está pendiente. La ubicación es la del
    /// registro y no cambia.
    /// </summary>
    public async Task<EventoDTO> ModificarAsync(int id, ModificarEventoDTO dto)
    {
        var evento = await BuscarPendienteAsync(id, "ya está resuelto: no se modifica");
        ExigirQueSeaSuyo(evento);
        // Un tipo que se desactivó después se puede conservar; uno nuevo tiene que estar activo.
        if (dto.TipoEventoId != evento.TipoEventoId) await ExigirTipoActivoAsync(dto.TipoEventoId);
        var (clienteId, pedidoId) = await VinculoAsync(dto);

        evento.TipoEventoId = dto.TipoEventoId;
        evento.ClienteId = clienteId;
        evento.PedidoId = pedidoId;
        evento.Descripcion = dto.Descripcion.Trim();
        await GuardarAsync("El evento cambió mientras lo modificabas. Vuelve a intentarlo.");
        return await ObtenerAsync(id);
    }

    // La regla del dueño no es un permiso: ver el evento por la cartera no alcanza para cambiarlo. Con cartera completa
    // (la secretaria) se cambia cualquiera.
    private void ExigirQueSeaSuyo(Evento evento)
    {
        if (!usuario.VeCarteraCompleta && evento.PromotorId != usuario.Id)
            throw new ReglaDeNegocioException("evento_de_otro_promotor",
                $"El evento {evento.Numero} lo registró otro promotor: solo quien lo registró lo cambia.");
    }

    /// <summary>
    /// Agregar una foto (4.2): solo mientras el evento está pendiente, y hasta 5 por evento. La ubicación y la hora de la
    /// toma las manda la app; el panel no.
    /// </summary>
    public async Task<EventoDTO> AgregarFotoAsync(int id, ArchivoRecibido archivo, decimal? latitud, decimal? longitud,
        DateTime? fechaCaptura)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await BloquearAsync(id);
        var evento = await BuscarPendienteAsync(id, "ya está resuelto: no recibe más fotos");
        ExigirQueSeaSuyo(evento);
        if (await db.Evidencias.CountAsync(f => f.EventoId == id) >= MaximoDeFotos)
            throw new ReglaDeNegocioException("limite_de_fotos",
                $"El evento ya tiene {MaximoDeFotos} fotos: elimina una para agregar otra.");

        var foto = await evidencias.GuardarFotoAsync(archivo, latitud, longitud,
            fechaCaptura is { } fecha ? HoraBolivia.AUtc(fecha) : null);
        try
        {
            foto.EventoId = id;
            db.Evidencias.Add(foto);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            evidencias.BorrarArchivo(foto.RutaAlmacenamiento);
            throw;
        }
        return await ObtenerAsync(id);
    }

    /// <summary>Eliminar una foto (4.2): solo mientras el evento está pendiente. El archivo se borra después de guardar.</summary>
    public async Task<EventoDTO> EliminarFotoAsync(int id, int fotoId)
    {
        string ruta;
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await BloquearAsync(id);
            var evento = await BuscarPendienteAsync(id, "ya está resuelto: sus fotos no se eliminan");
            ExigirQueSeaSuyo(evento);
            var foto = await db.Evidencias.FirstOrDefaultAsync(f => f.Id == fotoId && f.EventoId == id)
                ?? throw new NoEncontradoException("No se encontró la foto.");

            ruta = foto.RutaAlmacenamiento;
            db.Evidencias.Remove(foto);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        evidencias.BorrarArchivo(ruta);
        return await ObtenerAsync(id);
    }

    // Las fotos no cambian la fila del evento, así que su token de concurrencia no las ve. Agregar o quitar una foto y
    // resolver toman primero este bloqueo de la fila: así no se cruzan (por ejemplo, quitar la única foto mientras se
    // resuelve un evento que la exige). Hace falta una transacción abierta.
    private Task BloquearAsync(int id) =>
        db.Database.ExecuteSqlAsync($"SELECT Id FROM Eventos WHERE Id = {id} FOR UPDATE");

    /// <summary>
    /// Resolver (4.1): guarda la solución, la fecha y quién lo resolvió. Un evento resuelto ya no cambia. Si su tipo pide
    /// evidencia, hace falta al menos una foto: así se puede registrar primero y subir la foto después.
    /// </summary>
    public async Task<EventoDTO> ResolverAsync(int id, string solucion)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await BloquearAsync(id);
        var evento = await BuscarPendienteAsync(id, "ya está resuelto");
        var tipo = await db.TiposEvento.Where(t => t.Id == evento.TipoEventoId)
            .Select(t => new { t.Nombre, t.RequiereEvidencia }).FirstAsync();
        if (tipo.RequiereEvidencia && !await db.Evidencias.AnyAsync(f => f.EventoId == id))
            throw new ReglaDeNegocioException("evento_sin_evidencia",
                $"Un evento de tipo «{tipo.Nombre}» se resuelve con al menos una foto: agrégala antes de resolverlo.");

        evento.EstadoEvento = EstadoEvento.Resuelto;
        evento.Solucion = solucion.Trim();
        evento.ResueltoPorId = usuario.Id;
        evento.FechaResolucion = Ahora;
        await GuardarAsync("El evento cambió mientras lo resolvías. Vuelve a intentarlo.");
        await tx.CommitAsync();
        return await ObtenerAsync(id);
    }

    /// <summary>Un evento visible que todavía está pendiente: un evento resuelto no se modifica ni se resuelve otra vez.</summary>
    private async Task<Evento> BuscarPendienteAsync(int id, string siEstaResuelto)
    {
        var evento = await Visibles.FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new NoEncontradoException("No se encontró el evento.");
        if (evento.EstadoEvento == EstadoEvento.Resuelto)
            throw new ReglaDeNegocioException("evento_resuelto", $"El evento {evento.Numero} {siEstaResuelto}.");
        return evento;
    }

    // Otra petición cambió el evento entre que se leyó y se guardó.
    private async Task GuardarAsync(string mensajeDeConflicto)
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictoException("conflicto_concurrencia", mensajeDeConflicto);
        }
    }

    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;
}
