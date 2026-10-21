using ControlGestion.API.DTOs;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PedidosController(PedidoService pedidos) : ControllerBase
{
    // Quien opera sobre un pedido también necesita verlo: la lista, el detalle y el historial se abren con cualquiera
    // de estos permisos, siempre filtrados por cartera.
    [HttpGet]
    [Permiso(Permisos.Pedidos.ConsultarPedidos, Permisos.Pedidos.VerBandeja, Permisos.Pedidos.ModificarPedido,
        Permisos.Pedidos.AnularPedido, Permisos.Pedidos.CambiarEstado, Permisos.Pedidos.ReprogramarEntrega,
        Permisos.Pedidos.AsignarPedidos, Permisos.Pedidos.RegistrarEntrega)]
    public async Task<ActionResult<Pagina<PedidoListaDTO>>> GetAll([FromQuery] FiltrosPedidoDTO filtros) =>
        await pedidos.ListarAsync(filtros);

    [HttpGet("{id:int}")]
    [Permiso(Permisos.Pedidos.ConsultarPedidos, Permisos.Pedidos.VerBandeja, Permisos.Pedidos.ModificarPedido,
        Permisos.Pedidos.AnularPedido, Permisos.Pedidos.CambiarEstado, Permisos.Pedidos.ReprogramarEntrega,
        Permisos.Pedidos.AsignarPedidos, Permisos.Pedidos.RegistrarEntrega)]
    public async Task<ActionResult<PedidoDTO>> GetById(int id) =>
        await pedidos.ObtenerAsync(id);

    [HttpGet("{id:int}/historial")]
    [Permiso(Permisos.Pedidos.ConsultarPedidos, Permisos.Pedidos.VerBandeja, Permisos.Pedidos.ModificarPedido,
        Permisos.Pedidos.AnularPedido, Permisos.Pedidos.CambiarEstado, Permisos.Pedidos.ReprogramarEntrega,
        Permisos.Pedidos.AsignarPedidos, Permisos.Pedidos.RegistrarEntrega)]
    public async Task<ActionResult<List<HistorialPedidoDTO>>> GetHistorial(int id) =>
        await pedidos.HistorialAsync(id);

    [HttpGet("estados")]
    public async Task<ActionResult<List<EstadoPedidoDTO>>> GetEstados() =>
        await pedidos.EstadosAsync();

    [HttpPost]
    [Permiso(Permisos.Pedidos.RegistrarPedido)]
    public async Task<ActionResult<PedidoDTO>> Registrar([FromBody] RegistrarPedidoDTO dto)
    {
        var registrado = await pedidos.RegistrarAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = registrado.Id }, registrado);
    }

    [HttpPut("{id:int}")]
    [Permiso(Permisos.Pedidos.ModificarPedido)]
    public async Task<ActionResult<PedidoDTO>> Modificar(int id, [FromBody] ModificarPedidoDTO dto) =>
        await pedidos.ModificarAsync(id, dto);

    [HttpPost("{id:int}/reprogramar")]
    [Permiso(Permisos.Pedidos.ReprogramarEntrega)]
    public async Task<ActionResult<PedidoDTO>> Reprogramar(int id, [FromBody] ReprogramarPedidoDTO dto) =>
        await pedidos.ReprogramarAsync(id, dto);

    [HttpPost("{id:int}/avanzar")]
    [Permiso(Permisos.Pedidos.CambiarEstado)]
    public async Task<ActionResult<PedidoDTO>> Avanzar(int id, [FromBody] AvanzarPedidoDTO dto) =>
        await pedidos.AvanzarAsync(id, dto.Observacion);

    [HttpPost("{id:int}/anular")]
    [Permiso(Permisos.Pedidos.AnularPedido)]
    public async Task<ActionResult<PedidoDTO>> Anular(int id, [FromBody] AnularPedidoDTO dto) =>
        await pedidos.AnularAsync(id, dto.Motivo);

    // Hoy este endpoint hace las tres operaciones; se separa en los pasos siguientes de la tarea 08.
    [HttpPost("{id:int}/estado")]
    [Permiso(Permisos.Pedidos.CambiarEstado, Permisos.Pedidos.RegistrarEntrega, Permisos.Pedidos.AnularPedido)]
    public async Task<ActionResult<PedidoDTO>> CambiarEstado(int id, [FromBody] CambiarEstadoDTO dto) =>
        await pedidos.CambiarEstadoAsync(id, dto);
}
