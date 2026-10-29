using ControlGestion.API.DTOs;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RutasController(RutaService rutas) : ControllerBase
{
    [HttpGet]
    [Permiso(Permisos.Territorial.PlanificarRuta, Permisos.Territorial.SeguirRuta, Permisos.Territorial.RecorrerRuta)]
    public async Task<ActionResult<List<RutaListaDTO>>> GetAll([FromQuery] FiltrosRutaDTO filtros) =>
        await rutas.ListarAsync(filtros);

    [HttpGet("sugerida")]
    [Permiso(Permisos.Territorial.PlanificarRuta)]
    public async Task<ActionResult<List<ClienteSugeridoDTO>>> GetSugerida([FromQuery] SugeridaDTO dto) =>
        await rutas.SugeridaAsync(dto.ZonaId!.Value, dto.Fecha!.Value);

    // La ruta de hoy del promotor que la pide, para la app (7.3.20, paso 3). Sin ruta, 404 sin_ruta_del_dia.
    [HttpGet("del-dia")]
    [Permiso(Permisos.Territorial.RecorrerRuta)]
    public async Task<ActionResult<RutaDTO>> GetDelDia() =>
        await rutas.DelDiaAsync();

    [HttpGet("{id:int}")]
    [Permiso(Permisos.Territorial.PlanificarRuta, Permisos.Territorial.SeguirRuta, Permisos.Territorial.RecorrerRuta)]
    public async Task<ActionResult<RutaDTO>> GetById(int id) =>
        await rutas.ObtenerAsync(id);

    [HttpPost]
    [Permiso(Permisos.Territorial.PlanificarRuta)]
    public async Task<ActionResult<RutaDTO>> Planificar([FromBody] PlanificarRutaDTO dto)
    {
        var ruta = await rutas.PlanificarAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = ruta.Id }, ruta);
    }

    // Solo una ruta planificada: para corregirla se elimina y se planifica otra (no se modifica).
    [HttpDelete("{id:int}")]
    [Permiso(Permisos.Territorial.PlanificarRuta)]
    public async Task<IActionResult> Eliminar(int id)
    {
        await rutas.EliminarAsync(id);
        return NoContent();
    }

    // El recorrido es del promotor de la ruta, con la app (tarea 17): RutaService revisa que sea el dueño.
    [HttpPost("{id:int}/iniciar")]
    [Permiso(Permisos.Territorial.RecorrerRuta)]
    public async Task<ActionResult<RutaDTO>> Iniciar(int id) =>
        await rutas.IniciarAsync(id);

    [HttpPost("{id:int}/visitas/{visitaId:int}")]
    [Permiso(Permisos.Territorial.RecorrerRuta)]
    public async Task<ActionResult<RutaDTO>> MarcarVisita(int id, int visitaId, [FromBody] MarcarVisitaDTO dto) =>
        await rutas.MarcarVisitaAsync(id, visitaId, dto);

    // El camino por calles hasta el cliente de la visita (tarea 25), con un límite por usuario: el proveedor tiene un cupo
    // diario. Pasado el límite, o si el proveedor falla, 503 navegacion_no_disponible.
    [HttpGet("{id:int}/visitas/{visitaId:int}/camino")]
    [Permiso(Permisos.Territorial.RecorrerRuta)]
    [EnableRateLimiting(LimiteDeCaminos.Politica)]
    public async Task<ActionResult<CaminosDTO>> GetCamino(int id, int visitaId, [FromQuery] CaminoConsultaDTO consulta) =>
        await rutas.CaminoAsync(id, visitaId, consulta);

    // Parte C de la tarea 25: el orden más corto de lo que le falta al promotor, desde donde está. No guarda nada; tiene su
    // propio límite, porque usa otro cupo del proveedor.
    [HttpGet("{id:int}/orden-sugerido")]
    [Permiso(Permisos.Territorial.RecorrerRuta)]
    [EnableRateLimiting(LimiteDeCaminos.PoliticaDeOrdenes)]
    public async Task<ActionResult<OrdenSugeridoDTO>> GetOrdenSugerido(int id, [FromQuery] OrdenConsultaDTO consulta) =>
        await rutas.OrdenSugeridoAsync(id, consulta);

    [HttpPost("{id:int}/reordenar")]
    [Permiso(Permisos.Territorial.RecorrerRuta)]
    public async Task<ActionResult<RutaDTO>> Reordenar(int id, [FromBody] ReordenarDTO dto) =>
        await rutas.ReordenarAsync(id, dto);

    [HttpPost("{id:int}/ubicaciones")]
    // La app la manda sola cada 30 s con la ruta en curso: no es una operación del usuario.
    [SinBitacora]
    [Permiso(Permisos.Territorial.RecorrerRuta)]
    public async Task<IActionResult> RegistrarUbicacion(int id, [FromBody] RegistrarUbicacionDTO dto)
    {
        await rutas.RegistrarUbicacionAsync(id, dto);
        return NoContent();
    }

    [HttpPost("{id:int}/terminar")]
    [Permiso(Permisos.Territorial.RecorrerRuta)]
    public async Task<ActionResult<RutaDTO>> Terminar(int id) =>
        await rutas.TerminarAsync(id);
}
