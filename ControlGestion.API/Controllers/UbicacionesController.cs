using ControlGestion.API.DTOs;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

/// <summary>
/// La ubicación que la app manda sola mientras el promotor tiene entregas en ruta o su ruta en curso (tarea 31). El panel
/// la ve en "Entregas de hoy" (GET /pedidos/entregas) y en Rutas.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UbicacionesController(SeguimientoService seguimiento) : ControllerBase
{
    // Lo que la app sigue del promotor: con eso decide si manda su ubicación y cada cuánto.
    [HttpGet("seguimiento")]
    [Permiso(Permisos.Territorial.RecorrerRuta, Permisos.Pedidos.RegistrarEntrega)]
    public async Task<ActionResult<SeguimientoDTO>> GetSeguimiento() =>
        await seguimiento.DelUsuarioAsync();

    // La app la manda sola, cada 10 s con entregas en ruta o cada 30 s con la ruta: no es una operación del usuario. Sin nada
    // que seguir, 409 sin_seguimiento.
    [HttpPost]
    [SinBitacora]
    [Permiso(Permisos.Territorial.RecorrerRuta, Permisos.Pedidos.RegistrarEntrega)]
    public async Task<ActionResult<SeguimientoDTO>> Registrar([FromBody] RegistrarUbicacionDTO dto) =>
        await seguimiento.RegistrarAsync(dto);
}
