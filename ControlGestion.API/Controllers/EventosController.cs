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
public class EventosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public EventosController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? tipoEventoId,
        [FromQuery] EstadoEvento? estado,
        [FromQuery] int? clienteId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta)
    {
        var query = _db.Eventos
            .Include(e => e.TipoEvento)
            .Include(e => e.Cliente)
            .Include(e => e.Pedido)
            .Include(e => e.Promotor)
            .Include(e => e.Anexos)
            .AsQueryable();

        if (tipoEventoId.HasValue) query = query.Where(e => e.TipoEventoId == tipoEventoId);
        if (estado.HasValue) query = query.Where(e => e.EstadoEvento == estado);
        if (clienteId.HasValue) query = query.Where(e => e.ClienteId == clienteId);
        if (desde.HasValue) query = query.Where(e => e.FechaEvento >= desde);
        if (hasta.HasValue) query = query.Where(e => e.FechaEvento <= hasta);

        return Ok(await query.OrderByDescending(e => e.FechaEvento).ToListAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var evento = await _db.Eventos
            .Include(e => e.TipoEvento)
            .Include(e => e.Cliente)
            .Include(e => e.Pedido)
            .Include(e => e.Promotor)
            .Include(e => e.Anexos)
            .FirstOrDefaultAsync(e => e.Id == id);

        return Ok(evento ?? throw new NoEncontradoException("No se encontró el evento."));
    }

    [HttpGet("tipos")]
    public async Task<IActionResult> GetTipos() =>
        Ok(await _db.TiposEvento.Where(t => t.Activo).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EventoCreateDTO dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var contador = await _db.Eventos.CountAsync() + 1;

        var evento = new Evento
        {
            Numero = $"EVT-{DateTime.UtcNow:yyyy}-{contador:D5}",
            TipoEventoId = dto.TipoEventoId,
            ClienteId = dto.ClienteId,
            PedidoId = dto.PedidoId,
            PromotorId = userId,
            FechaEvento = DateTime.UtcNow,
            Latitud = dto.Latitud,
            Longitud = dto.Longitud,
            Descripcion = dto.Descripcion,
            EstadoEvento = EstadoEvento.Registrado
        };

        _db.Eventos.Add(evento);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = evento.Id }, evento);
    }

    [HttpPost("{id}/resolver")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<IActionResult> Resolver(int id, [FromBody] ResolverEventoDTO dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var evento = await _db.Eventos.FindAsync(id);
        if (evento == null) throw new NoEncontradoException("No se encontró el evento.");

        evento.EstadoEvento = EstadoEvento.Resuelto;
        evento.ResueltoPorId = userId;
        evento.FechaResolucion = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { evento.Id, evento.EstadoEvento });
    }

    [HttpPost("{id}/anexos")]
    public async Task<IActionResult> SubirAnexo(int id, IFormFile archivo, [FromForm] string? descripcion)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var evento = await _db.Eventos.FindAsync(id);
        if (evento == null) throw new NoEncontradoException("No se encontró el evento.");

        if (archivo == null || archivo.Length == 0)
            throw new ReglaDeNegocioException("archivo_requerido", "Archivo requerido.");

        var tiposPermitidos = new[] { "image/jpeg", "image/png", "image/webp", "application/pdf" };
        if (!tiposPermitidos.Contains(archivo.ContentType))
            throw new ReglaDeNegocioException("tipo_archivo_no_permitido", "Tipo de archivo no permitido.");

        var carpeta = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads",
            DateTime.UtcNow.Year.ToString(), DateTime.UtcNow.Month.ToString("D2"), "eventos");
        Directory.CreateDirectory(carpeta);

        var nombreArchivo = $"{Guid.NewGuid()}{Path.GetExtension(archivo.FileName)}";
        var rutaFisica = Path.Combine(carpeta, nombreArchivo);

        using (var stream = new FileStream(rutaFisica, FileMode.Create))
            await archivo.CopyToAsync(stream);

        var anexo = new Anexo
        {
            TablaOrigen = "Evento",
            RegistroOrigenId = id,
            EventoId = id,
            NombreArchivo = archivo.FileName,
            RutaAlmacenamiento = $"/uploads/{DateTime.UtcNow.Year}/{DateTime.UtcNow.Month:D2}/eventos/{nombreArchivo}",
            TipoMIME = archivo.ContentType,
            TamanoBytes = archivo.Length,
            SubidoPorId = userId,
            EsImagen = archivo.ContentType.StartsWith("image/"),
            Descripcion = descripcion
        };

        _db.Anexos.Add(anexo);
        await _db.SaveChangesAsync();
        return Ok(anexo);
    }
}
