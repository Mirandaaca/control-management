using ControlGestion.API.Data;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RutasController : ControllerBase
{
    private readonly AppDbContext _db;

    public RutasController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? promotorId,
        [FromQuery] int? zonaId,
        [FromQuery] EstadoRuta? estado,
        [FromQuery] DateTime? fecha)
    {
        var query = _db.Rutas
            .Include(r => r.Promotor)
            .Include(r => r.Zona)
            .Include(r => r.Detalles)
            .AsQueryable();

        if (!string.IsNullOrEmpty(promotorId)) query = query.Where(r => r.PromotorId == promotorId);
        if (zonaId.HasValue) query = query.Where(r => r.ZonaId == zonaId);
        if (estado.HasValue) query = query.Where(r => r.Estado == estado);
        if (fecha.HasValue) query = query.Where(r => r.FechaPlanificada.Date == fecha.Value.Date);

        var list = await query.OrderByDescending(r => r.FechaPlanificada).ToListAsync();

        return Ok(list.Select(r => new
        {
            r.Id, r.Numero, r.Nombre, r.Estado,
            r.FechaPlanificada, r.FechaInicio, r.FechaFin,
            Promotor = r.Promotor == null ? null : new { r.Promotor.Id, r.Promotor.NombreCompleto },
            Zona = r.Zona == null ? null : new { r.Zona.Id, r.Zona.Nombre },
            TotalClientes = r.Detalles.Count,
            Visitados = r.Detalles.Count(d => d.EstadoVisita == EstadoVisita.Visitado)
        }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var ruta = await _db.Rutas
            .Include(r => r.Promotor)
            .Include(r => r.Zona)
            .Include(r => r.Detalles).ThenInclude(d => d.Cliente).ThenInclude(c => c!.EstadoCliente)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (ruta == null) throw new NoEncontradoException("No se encontró la ruta.");

        return Ok(new
        {
            ruta.Id, ruta.Numero, ruta.Nombre, ruta.Estado, ruta.Observaciones,
            ruta.FechaPlanificada, ruta.FechaInicio, ruta.FechaFin,
            Promotor = ruta.Promotor == null ? null : new { ruta.Promotor.Id, ruta.Promotor.NombreCompleto },
            Zona = ruta.Zona == null ? null : new { ruta.Zona.Id, ruta.Zona.Nombre },
            Detalles = ruta.Detalles.OrderBy(d => d.Orden).Select(d => new
            {
                d.Id, d.Orden, d.EstadoVisita, d.HoraVisita, d.Latitud, d.Longitud, d.Observaciones,
                Cliente = d.Cliente == null ? null : new
                {
                    d.Cliente.Id, d.Cliente.NombreCompleto, d.Cliente.DireccionTexto,
                    d.Cliente.Latitud, d.Cliente.Longitud,
                    EstadoCliente = d.Cliente.EstadoCliente == null ? null : new { d.Cliente.EstadoCliente.Nombre, d.Cliente.EstadoCliente.Color }
                }
            })
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<IActionResult> Create([FromBody] RutaCreateDTO dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var clienteIds = dto.Clientes.Select(c => c.ClienteId).ToList();
        var clientesExisten = await _db.Clientes
            .Where(c => clienteIds.Contains(c.Id))
            .CountAsync();

        if (clientesExisten != clienteIds.Distinct().Count())
            throw new ReglaDeNegocioException("cliente_no_existe", "Uno o más clientes no existen.");

        var contador = await _db.Rutas.CountAsync() + 1;
        var ruta = new Ruta
        {
            Numero = $"RUT-{DateTime.UtcNow:yyyy}-{contador:D5}",
            Nombre = dto.Nombre,
            PromotorId = userId,
            ZonaId = dto.ZonaId,
            FechaPlanificada = dto.FechaPlanificada,
            Observaciones = dto.Observaciones,
            Estado = EstadoRuta.Planificada
        };

        foreach (var item in dto.Clientes)
        {
            ruta.Detalles.Add(new RutaDetalle
            {
                ClienteId = item.ClienteId,
                Orden = item.Orden
            });
        }

        _db.Rutas.Add(ruta);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = ruta.Id }, new { ruta.Id, ruta.Numero });
    }

    [HttpPost("{id}/iniciar")]
    public async Task<IActionResult> Iniciar(int id)
    {
        var ruta = await _db.Rutas.FindAsync(id);
        if (ruta == null) throw new NoEncontradoException("No se encontró la ruta.");
        if (ruta.Estado != EstadoRuta.Planificada)
            throw new ReglaDeNegocioException("ruta_no_planificada", "Solo se pueden iniciar rutas planificadas.");

        ruta.Estado = EstadoRuta.EnCurso;
        ruta.FechaInicio = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { ruta.Id, ruta.Estado });
    }

    [HttpPost("{id}/completar")]
    public async Task<IActionResult> Completar(int id)
    {
        var ruta = await _db.Rutas.FindAsync(id);
        if (ruta == null) throw new NoEncontradoException("No se encontró la ruta.");
        if (ruta.Estado != EstadoRuta.EnCurso)
            throw new ReglaDeNegocioException("ruta_no_en_curso", "Solo se pueden completar rutas en curso.");

        ruta.Estado = EstadoRuta.Completada;
        ruta.FechaFin = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { ruta.Id, ruta.Estado });
    }

    [HttpPost("{rutaId}/detalles/{detalleId}/visita")]
    public async Task<IActionResult> MarcarVisita(int rutaId, int detalleId, [FromBody] MarcarVisitaDTO dto)
    {
        var detalle = await _db.RutasDetalle
            .Include(d => d.Ruta)
            .FirstOrDefaultAsync(d => d.Id == detalleId && d.RutaId == rutaId);

        if (detalle == null) throw new NoEncontradoException("No se encontró la visita de la ruta.");
        if (detalle.Ruta!.Estado != EstadoRuta.EnCurso)
            throw new ReglaDeNegocioException("ruta_no_en_curso", "La ruta no está en curso.");

        detalle.EstadoVisita = (EstadoVisita)dto.EstadoVisita;
        detalle.HoraVisita = DateTime.UtcNow;
        detalle.Latitud = dto.Latitud;
        detalle.Longitud = dto.Longitud;
        detalle.Observaciones = dto.Observaciones;

        await _db.SaveChangesAsync();
        return Ok(new { detalle.Id, detalle.EstadoVisita });
    }
}
