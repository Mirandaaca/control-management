using ControlGestion.API.Data;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ZonasController : ControllerBase
{
    private readonly AppDbContext _context;

    public ZonasController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _context.Zonas.Include(z => z.Supervisor).ToListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var zona = await _context.Zonas.Include(z => z.Supervisor).FirstOrDefaultAsync(z => z.Id == id);
        return Ok(zona ?? throw new NoEncontradoException("No se encontró la zona."));
    }

    [HttpPost]
    [Permiso(Permisos.Parametros.RegistrarZona)]
    public async Task<IActionResult> Create([FromBody] Zona zona)
    {
        _context.Zonas.Add(zona);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = zona.Id }, zona);
    }

    [HttpPut("{id}")]
    [Permiso(Permisos.Parametros.ModificarZona)]
    public async Task<IActionResult> Update(int id, [FromBody] Zona zona)
    {
        if (id != zona.Id) throw new ReglaDeNegocioException("id_no_coincide", "El id de la URL no coincide con el del cuerpo.");
        if (!await _context.Zonas.AnyAsync(z => z.Id == id)) throw new NoEncontradoException("No se encontró la zona.");
        _context.Entry(zona).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Permiso(Permisos.Parametros.EliminarZona)]
    public async Task<IActionResult> Delete(int id)
    {
        var zona = await _context.Zonas.FindAsync(id);
        if (zona == null) throw new NoEncontradoException("No se encontró la zona.");
        _context.Zonas.Remove(zona);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
