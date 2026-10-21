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

    public async Task<EventoDTO> RegistrarAsync(RegistrarEventoDTO dto)
    {
        var evento = new Evento
        {
            Numero = await numerador.SiguienteDelAnioAsync(NumeradorService.Evento),
            TipoEventoId = dto.TipoEventoId,
            ClienteId = dto.ClienteId,
            PedidoId = dto.PedidoId,
            PromotorId = usuario.Id,
            FechaEvento = Ahora,
            Latitud = dto.Latitud,
            Longitud = dto.Longitud,
            Descripcion = dto.Descripcion,
            EstadoEvento = EstadoEvento.Registrado
        };

        db.Eventos.Add(evento);
        await db.SaveChangesAsync();
        return await ObtenerAsync(evento.Id);
    }

    public async Task<EventoDTO> ResolverAsync(int id)
    {
        var evento = await Visibles.FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new NoEncontradoException("No se encontró el evento.");

        evento.EstadoEvento = EstadoEvento.Resuelto;
        evento.ResueltoPorId = usuario.Id;
        evento.FechaResolucion = Ahora;
        await db.SaveChangesAsync();
        return await ObtenerAsync(id);
    }

    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;
}
