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
        Ok(await _context.Zonas.Include(z => z.Promotor).ToListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var zona = await _context.Zonas.Include(z => z.Promotor).FirstOrDefaultAsync(z => z.Id == id);
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
        var promotorAnterior = await _context.Zonas.Where(z => z.Id == id).Select(z => new { z.PromotorId }).FirstOrDefaultAsync()
            ?? throw new NoEncontradoException("No se encontró la zona.");

        await using var transaccion = await _context.Database.BeginTransactionAsync();
        _context.Entry(zona).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        // 7.2: el cliente siempre tiene el promotor de su zona. Las HP, cobranzas y pedidos ya registrados conservan el suyo.
        if (promotorAnterior.PromotorId != zona.PromotorId)
            await _context.Clientes.Where(c => c.ZonaId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.PromotorId, zona.PromotorId));
        await transaccion.CommitAsync();
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
