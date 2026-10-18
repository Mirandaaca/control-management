using ControlGestion.API.Data;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class ConfiguracionesController : ControllerBase
{
    private readonly AppDbContext _context;

    public ConfiguracionesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _context.ConfiguracionesGenerales.ToListAsync());

    [HttpGet("{clave}")]
    public async Task<IActionResult> GetByClave(string clave)
    {
        var config = await _context.ConfiguracionesGenerales.FindAsync(clave);
        return Ok(config ?? throw new NoEncontradoException("No se encontró la configuración."));
    }

    [HttpPut("{clave}")]
    public async Task<IActionResult> Upsert(string clave, [FromBody] ConfiguracionGeneral config)
    {
        config.Clave = clave;
        var existe = await _context.ConfiguracionesGenerales.AnyAsync(c => c.Clave == clave);
        if (existe)
            _context.Entry(config).State = EntityState.Modified;
        else
            _context.ConfiguracionesGenerales.Add(config);

        await _context.SaveChangesAsync();
        return Ok(config);
    }

    [HttpDelete("{clave}")]
    public async Task<IActionResult> Delete(string clave)
    {
        var config = await _context.ConfiguracionesGenerales.FindAsync(clave);
        if (config == null) throw new NoEncontradoException("No se encontró la configuración.");
        _context.ConfiguracionesGenerales.Remove(config);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
