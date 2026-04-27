using ControlGestion.API.Data;
using ControlGestion.API.Models;
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
        return zona == null ? NotFound() : Ok(zona);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] Zona zona)
    {
        _context.Zonas.Add(zona);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = zona.Id }, zona);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] Zona zona)
    {
        if (id != zona.Id) return BadRequest();
        _context.Entry(zona).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var zona = await _context.Zonas.FindAsync(id);
        if (zona == null) return NotFound();
        _context.Zonas.Remove(zona);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
