using ControlGestion.API.DTOs;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EventosController(EventoService eventos) : ControllerBase
{
    // Quien opera sobre un evento también necesita verlo: la lista y el detalle se abren con cualquiera de estos
    // permisos, siempre filtrados por cartera. El gerente general entra por el mapa hasta que tenga su pantalla.
    [HttpGet]
    [Permiso(Permisos.Eventos.ConsultarEventos, Permisos.Eventos.RegistrarEvento, Permisos.Eventos.ModificarEvento,
        Permisos.Eventos.ResolverEvento, Permisos.Eventos.AgregarFoto, Permisos.Eventos.EliminarFoto, Permisos.Eventos.VerMapa)]
    public async Task<ActionResult<Pagina<EventoListaDTO>>> GetAll([FromQuery] FiltrosEventoDTO filtros) =>
        await eventos.ListarAsync(filtros);

    // El detalle también se abre desde un punto del mapa.
    [HttpGet("{id:int}")]
    [Permiso(Permisos.Eventos.ConsultarEventos, Permisos.Eventos.RegistrarEvento, Permisos.Eventos.ModificarEvento,
        Permisos.Eventos.ResolverEvento, Permisos.Eventos.AgregarFoto, Permisos.Eventos.EliminarFoto, Permisos.Eventos.VerMapa)]
    public async Task<ActionResult<EventoDTO>> GetById(int id) =>
        await eventos.ObtenerAsync(id);

    [HttpGet("tipos")]
    public async Task<ActionResult<List<TipoEventoDTO>>> GetTipos() =>
        await eventos.TiposAsync();

    [HttpPost]
    [Permiso(Permisos.Eventos.RegistrarEvento)]
    public async Task<ActionResult<EventoDTO>> Registrar([FromBody] RegistrarEventoDTO dto)
    {
        var registrado = await eventos.RegistrarAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = registrado.Id }, registrado);
    }

    [HttpPost("{id:int}/resolver")]
    [Permiso(Permisos.Eventos.ResolverEvento)]
    public async Task<ActionResult<EventoDTO>> Resolver(int id, [FromBody] ResolverEventoDTO dto) =>
        await eventos.ResolverAsync(id, dto.Solucion);
}
