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
public class EventoService(AppDbContext db, IUsuarioActual usuario, NumeradorService numerador, TimeProvider reloj)
{
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

    /// <summary>Resolver (4.1): guarda la solución, la fecha y quién lo resolvió. Un evento resuelto ya no cambia.</summary>
    public async Task<EventoDTO> ResolverAsync(int id, string solucion)
    {
        var evento = await BuscarPendienteAsync(id, "ya está resuelto");

        evento.EstadoEvento = EstadoEvento.Resuelto;
        evento.Solucion = solucion.Trim();
        evento.ResueltoPorId = usuario.Id;
        evento.FechaResolucion = Ahora;
        await GuardarAsync("El evento cambió mientras lo resolvías. Vuelve a intentarlo.");
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
