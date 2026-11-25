using ControlGestion.API.DTOs;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

/// <summary>
/// Los tipos de evento (7.3.13, tarea 32). El formulario del evento sigue leyendo los activos de GET /eventos/tipos, que
/// no cambió (lo usan los APK instalados). Activar, desactivar y eliminar comparten un permiso: cada uno le dice a la
/// bitácora qué operación es.
/// </summary>
[ApiController]
[Route("api/tipos-evento")]
[Authorize]
public class TiposEventoController(TipoEventoService tipos) : ControllerBase
{
    [HttpGet]
    [Permiso(Permisos.Parametros.RegistrarTipoEvento, Permisos.Parametros.ModificarTipoEvento, Permisos.Parametros.DesactivarTipoEvento)]
    public async Task<ActionResult<List<TipoEventoAdminDTO>>> GetAll() =>
        await tipos.ListarAsync();

    // Los filtros de Eventos, el Mapa de eventos y el Reporte de incidencias: quien abre alguna de esas pantallas.
    [HttpGet("opciones")]
    [Permiso(Permisos.Eventos.ConsultarEventos, Permisos.Eventos.RegistrarEvento, Permisos.Eventos.ModificarEvento,
        Permisos.Eventos.ResolverEvento, Permisos.Eventos.AgregarFoto, Permisos.Eventos.EliminarFoto, Permisos.Eventos.VerMapa,
        Permisos.Eventos.GenerarReporte)]
    public async Task<ActionResult<List<TipoEventoOpcionDTO>>> GetOpciones() =>
        await tipos.OpcionesAsync();

    [HttpGet("{id:int}")]
    [Permiso(Permisos.Parametros.RegistrarTipoEvento, Permisos.Parametros.ModificarTipoEvento, Permisos.Parametros.DesactivarTipoEvento)]
    public async Task<ActionResult<TipoEventoAdminDTO>> GetById(int id) =>
        await tipos.ObtenerAsync(id);

    [HttpPost]
    [Permiso(Permisos.Parametros.RegistrarTipoEvento)]
    public async Task<ActionResult<TipoEventoAdminDTO>> Registrar([FromBody] GuardarTipoEventoDTO dto)
    {
        var creado = await tipos.RegistrarAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:int}")]
    [Permiso(Permisos.Parametros.ModificarTipoEvento)]
    public async Task<ActionResult<TipoEventoAdminDTO>> Modificar(int id, [FromBody] GuardarTipoEventoDTO dto) =>
        await tipos.ModificarAsync(id, dto);

    [HttpPut("{id:int}/activar")]
    [Permiso(Permisos.Parametros.DesactivarTipoEvento)]
    [Bitacora("Activar tipos de evento")]
    public async Task<ActionResult<TipoEventoAdminDTO>> Activar(int id) =>
        await tipos.ActivarAsync(id);

    [HttpPut("{id:int}/desactivar")]
    [Permiso(Permisos.Parametros.DesactivarTipoEvento)]
    [Bitacora("Desactivar tipos de evento")]
    public async Task<ActionResult<TipoEventoAdminDTO>> Desactivar(int id) =>
        await tipos.DesactivarAsync(id);

    [HttpDelete("{id:int}")]
    [Permiso(Permisos.Parametros.DesactivarTipoEvento)]
    [Bitacora("Eliminar tipos de evento")]
    public async Task<IActionResult> Eliminar(int id)
    {
        await tipos.EliminarAsync(id);
        return NoContent();
    }
}
