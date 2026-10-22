using System.Linq.Expressions;
using ControlGestion.API.Data;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>
/// Administración de los estados del cliente (7.4). Activo y Disponible para re-visita son del sistema: solo cambia su
/// descripción. Inactivo es manual, pero el código depende de él: no se elimina ni se renombra. Los estados manuales
/// eligen su color de la paleta, y uno que algún cliente tiene no se elimina.
/// </summary>
public class EstadoClienteService(AppDbContext db)
{
    // Protegido sigue la misma regla que Protegido(estado), escrita para que la traduzca la base.
    private Expression<Func<EstadoCliente, EstadoClienteAdminDTO>> ComoAdmin =>
        e => new EstadoClienteAdminDTO(e.Id, e.Nombre, e.Color, e.Descripcion, e.EsDelSistema,
            e.EsDelSistema || e.Id == EstadosDeCliente.Inactivo, db.Clientes.Count(c => c.EstadoClienteId == e.Id));

    public Task<List<EstadoClienteAdminDTO>> ListarAsync() =>
        db.EstadosCliente.OrderBy(e => e.Id).Select(ComoAdmin).ToListAsync();

    public async Task<EstadoClienteAdminDTO> ObtenerAsync(int id) =>
        await db.EstadosCliente.Where(e => e.Id == id).Select(ComoAdmin).FirstOrDefaultAsync()
        ?? throw new NoEncontradoException("No se encontró el estado.");

    public async Task<EstadoClienteAdminDTO> RegistrarAsync(GuardarEstadoClienteDTO dto)
    {
        var estado = new EstadoCliente { Nombre = dto.Nombre.Trim(), Color = ColorDeLaPaleta(dto.Color), Descripcion = Limpiar(dto.Descripcion) };
        await ExigirNombreLibreAsync(estado.Nombre, idPropio: null);
        db.EstadosCliente.Add(estado);
        await db.SaveChangesAsync();
        return await ObtenerAsync(estado.Id);
    }

    public async Task<EstadoClienteAdminDTO> ModificarAsync(int id, GuardarEstadoClienteDTO dto)
    {
        var estado = await db.EstadosCliente.FindAsync(id) ?? throw new NoEncontradoException("No se encontró el estado.");

        var nombre = dto.Nombre.Trim();
        if (nombre != estado.Nombre)
        {
            if (Protegido(estado))
                throw new ReglaDeNegocioException("estado_protegido", $"{estado.Nombre} no cambia de nombre: {PorQueEstaProtegido(estado)}.");
            await ExigirNombreLibreAsync(nombre, id);
            estado.Nombre = nombre;
        }

        if (!estado.EsDelSistema)
            estado.Color = ColorDeLaPaleta(dto.Color);
        else if (!string.Equals(dto.Color.Trim(), estado.Color, StringComparison.OrdinalIgnoreCase))
            throw new ReglaDeNegocioException("estado_protegido",
                "El rojo de Activo y el verde de Disponible para re-visita tienen un significado fijo (5.2): su color no se cambia.");

        estado.Descripcion = Limpiar(dto.Descripcion);
        await db.SaveChangesAsync();
        return await ObtenerAsync(id);
    }

    public async Task EliminarAsync(int id)
    {
        var estado = await db.EstadosCliente.FindAsync(id) ?? throw new NoEncontradoException("No se encontró el estado.");
        if (Protegido(estado))
            throw new ReglaDeNegocioException("estado_protegido", $"{estado.Nombre} no se elimina: {PorQueEstaProtegido(estado)}.");

        var clientes = await db.Clientes.CountAsync(c => c.EstadoClienteId == id);
        if (clientes > 0)
            throw new ConflictoException("estado_en_uso", clientes == 1
                ? "Hay 1 cliente con este estado: asígnale otro antes de eliminarlo."
                : $"Hay {clientes} clientes con este estado: asígnales otro antes de eliminarlo.");

        db.EstadosCliente.Remove(estado);
        await db.SaveChangesAsync();
    }

    private static bool Protegido(EstadoCliente estado) => estado.EsDelSistema || estado.Id == EstadosDeCliente.Inactivo;

    private static string PorQueEstaProtegido(EstadoCliente estado) => estado.EsDelSistema
        ? "el sistema lo asigna según las visitas"
        : "el sistema lo usa para bloquear las ventas y los pedidos del cliente";

    // La intercalación de la base no distingue mayúsculas ni tildes: "activo" choca con "Activo". El índice único frena
    // además dos registros a la vez.
    private async Task ExigirNombreLibreAsync(string nombre, int? idPropio)
    {
        if (await db.EstadosCliente.AnyAsync(e => e.Nombre == nombre && e.Id != idPropio))
            throw new ConflictoException("estado_repetido", $"Ya existe el estado \"{nombre}\".");
    }

    private static string ColorDeLaPaleta(string color) =>
        PaletaDeEstados.Normalizar(color)
        ?? throw new ValidacionException(new Dictionary<string, string[]> { ["color"] = ["Elige un color de la paleta."] });

    private static string? Limpiar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
