using ControlGestion.API.Data;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly NumeradorService _numerador;

    public ClientesController(AppDbContext db, NumeradorService numerador)
    {
        _db = db;
        _numerador = numerador;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? zonaId,
        [FromQuery] int? estadoId,
        [FromQuery] string? promotorId,
        [FromQuery] bool? soloRevisita)
    {
        var query = _db.Clientes
            .Include(c => c.Zona)
            .Include(c => c.EstadoCliente)
            .Include(c => c.Promotor)
            .Where(c => c.Activo)
            .AsQueryable();

        if (zonaId.HasValue) query = query.Where(c => c.ZonaId == zonaId);
        if (estadoId.HasValue) query = query.Where(c => c.EstadoClienteId == estadoId);
        if (!string.IsNullOrEmpty(promotorId)) query = query.Where(c => c.PromotorId == promotorId);
        if (soloRevisita == true) query = query.Where(c => c.ProximaVisita <= DateTime.UtcNow);

        return Ok(await query.OrderBy(c => c.NombreCompleto).ToListAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var cliente = await _db.Clientes
            .Include(c => c.Zona)
            .Include(c => c.EstadoCliente)
            .Include(c => c.Promotor)
            .FirstOrDefaultAsync(c => c.Id == id && c.Activo);

        return Ok(cliente ?? throw new NoEncontradoException("No se encontró el cliente."));
    }

    [HttpGet("mapa")]
    public async Task<IActionResult> GetParaMapa([FromQuery] int? zonaId)
    {
        var query = _db.Clientes
            .Include(c => c.EstadoCliente)
            .Where(c => c.Activo && c.Latitud != 0 && c.Longitud != 0)
            .AsQueryable();

        if (zonaId.HasValue) query = query.Where(c => c.ZonaId == zonaId);

        var result = await query.Select(c => new
        {
            c.Id, c.Codigo, c.NombreCompleto, c.Latitud, c.Longitud,
            c.Telefono, c.DireccionTexto, c.ProximaVisita,
            Estado = new { c.EstadoCliente!.Nombre, c.EstadoCliente.Color }
        }).ToListAsync();

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<IActionResult> Create([FromBody] ClienteCreateDTO dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var cliente = new Cliente
        {
            Codigo = await _numerador.SiguienteAsync(NumeradorService.Cliente),
            NombreCompleto = dto.NombreCompleto,
            RazonSocial = dto.RazonSocial,
            NIT = dto.NIT,
            Telefono = dto.Telefono,
            Email = dto.Email,
            DireccionTexto = dto.DireccionTexto,
            Latitud = dto.Latitud,
            Longitud = dto.Longitud,
            ZonaId = dto.ZonaId,
            EstadoClienteId = 1,
            PromotorId = string.IsNullOrEmpty(dto.PromotorId) ? null : dto.PromotorId,
            DiasRevisita = dto.DiasRevisita,
            TieneCredito = dto.TieneCredito,
            LimiteCredito = dto.LimiteCredito,
            CreadoPorId = userId
        };

        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = cliente.Id }, cliente);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<IActionResult> Update(int id, [FromBody] ClienteUpdateDTO dto)
    {
        var cliente = await _db.Clientes.FindAsync(id);
        if (cliente == null) throw new NoEncontradoException("No se encontró el cliente.");

        cliente.NombreCompleto = dto.NombreCompleto;
        cliente.RazonSocial = dto.RazonSocial;
        cliente.NIT = dto.NIT;
        cliente.Telefono = dto.Telefono;
        cliente.Email = dto.Email;
        cliente.DireccionTexto = dto.DireccionTexto;
        cliente.Latitud = dto.Latitud;
        cliente.Longitud = dto.Longitud;
        cliente.ZonaId = dto.ZonaId;
        cliente.EstadoClienteId = dto.EstadoClienteId;
        cliente.PromotorId = string.IsNullOrEmpty(dto.PromotorId) ? null : dto.PromotorId;
        cliente.DiasRevisita = dto.DiasRevisita;
        cliente.TieneCredito = dto.TieneCredito;
        cliente.LimiteCredito = dto.LimiteCredito;

        await _db.SaveChangesAsync();
        return Ok(cliente);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var cliente = await _db.Clientes.FindAsync(id);
        if (cliente == null) throw new NoEncontradoException("No se encontró el cliente.");
        cliente.Activo = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("estados")]
    public async Task<IActionResult> GetEstados() =>
        Ok(await _db.EstadosCliente.Where(e => e.Activo).ToListAsync());
}
