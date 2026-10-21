using ControlGestion.API.DTOs;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/hojas-pedido")]
[Authorize]
public class HojasPedidoController(HojaPedidoService hojas) : ControllerBase
{
    // Quien anula también necesita ver la lista y el detalle.
    [HttpGet]
    [Permiso(Permisos.Ventas.ConsultarHp, Permisos.Ventas.AnularHp)]
    public async Task<ActionResult<Pagina<HojaPedidoListaDTO>>> GetAll([FromQuery] FiltrosHojaPedidoDTO filtros) =>
        await hojas.ListarAsync(filtros);

    [HttpGet("{id:int}")]
    [Permiso(Permisos.Ventas.ConsultarHp, Permisos.Ventas.AnularHp)]
    public async Task<ActionResult<HojaPedidoDTO>> GetById(int id) =>
        await hojas.ObtenerAsync(id);

    [HttpPost]
    [Permiso(Permisos.Ventas.RegistrarHp)]
    public async Task<ActionResult<HojaPedidoDTO>> Registrar([FromBody] RegistrarHojaPedidoDTO dto)
    {
        var registrada = await hojas.RegistrarAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = registrada.Id }, registrada);
    }

    [HttpPost("{id:int}/confirmar")]
    [Permiso(Permisos.Ventas.ConfirmarHp)]
    public async Task<ActionResult<HojaPedidoDTO>> Confirmar(int id) =>
        await hojas.ConfirmarAsync(id);

    [HttpPost("{id:int}/anular")]
    [Permiso(Permisos.Ventas.AnularHp)]
    public async Task<ActionResult<HojaPedidoDTO>> Anular(int id, [FromBody] AnularHojaPedidoDTO dto) =>
        await hojas.AnularAsync(id, dto.Motivo);
}
