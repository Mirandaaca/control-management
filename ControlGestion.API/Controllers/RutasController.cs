using ControlGestion.API.DTOs;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
}
