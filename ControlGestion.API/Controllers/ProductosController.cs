using ControlGestion.API.Data;
using ControlGestion.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductosController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProductosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _context.Productos.Where(p => p.Activo).ToListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var producto = await _context.Productos.FindAsync(id);
        return producto == null ? NotFound() : Ok(producto);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<IActionResult> Create([FromBody] Producto producto)
    {
        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = producto.Id }, producto);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<IActionResult> Update(int id, [FromBody] Producto producto)
    {
        if (id != producto.Id) return BadRequest();
        _context.Entry(producto).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var producto = await _context.Productos.FindAsync(id);
        if (producto == null) return NotFound();
        producto.Activo = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
