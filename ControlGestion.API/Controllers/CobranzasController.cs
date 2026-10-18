using ControlGestion.API.DTOs;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CobranzasController(CobranzaService cobranzas) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CobranzaResumenDTO>>> GetAll([FromQuery] FiltrosCobranzaDTO filtros) =>
        await cobranzas.ListarAsync(filtros);

    [HttpGet("{id}")]
    public async Task<ActionResult<CobranzaDetalleDTO>> GetById(int id) =>
        await cobranzas.ObtenerAsync(id);

    [HttpPost("{id}/pagos")]
    public async Task<ActionResult<PagoRegistradoDTO>> RegistrarPago(int id, [FromBody] RegistrarPagoDTO dto) =>
        await cobranzas.RegistrarPagoAsync(id, dto);

    [HttpPost("{id}/incobrable")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<ActionResult<CobranzaDetalleDTO>> MarcarIncobrable(int id, [FromBody] MarcarIncobrableDTO dto) =>
        await cobranzas.MarcarIncobrableAsync(id, dto.Observacion);
}
