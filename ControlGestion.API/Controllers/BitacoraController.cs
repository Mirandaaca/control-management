using ControlGestion.API.DTOs;
using ControlGestion.API.Services;
using ControlGestion.API.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

// La bitácora (8.5) solo se lee: no hay endpoints para modificar ni eliminar sus registros.
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Permiso(Permisos.Seguridad.ConsultarBitacora)]
public class BitacoraController(BitacoraService bitacora) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Pagina<RegistroBitacoraDTO>>> Get([FromQuery] FiltrosBitacoraDTO filtros) =>
        await bitacora.ConsultarAsync(filtros);

    [HttpGet("opciones")]
    public async Task<ActionResult<OpcionesBitacoraDTO>> GetOpciones() =>
        await bitacora.OpcionesAsync();

    [HttpGet("excel")]
    public async Task<IActionResult> GetExcel([FromQuery] FiltrosBitacoraDTO filtros)
    {
        var (contenido, nombre) = await bitacora.ExcelAsync(filtros);
        return File(contenido, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
    }
}
