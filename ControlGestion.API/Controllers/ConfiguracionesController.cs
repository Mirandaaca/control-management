using ControlGestion.API.DTOs;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConfiguracionesController(ConfiguracionService configuracion) : ControllerBase
{
    [HttpGet("dias-revisita")]
    [Permiso(Permisos.Parametros.ModificarDiasRevisita)]
    public async Task<ActionResult<DiasRevisitaDTO>> GetDiasRevisita() =>
        await configuracion.DiasRevisitaAsync();

    [HttpPut("dias-revisita")]
    [Permiso(Permisos.Parametros.ModificarDiasRevisita)]
    public async Task<ActionResult<DiasRevisitaDTO>> CambiarDiasRevisita([FromBody] CambiarDiasRevisitaDTO dto) =>
        await configuracion.CambiarDiasRevisitaAsync(dto.Dias);
}
