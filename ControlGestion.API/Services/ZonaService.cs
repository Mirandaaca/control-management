using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>Zonas de venta (7.2): cada una con su promotor, que es el de todos sus clientes.</summary>
public class ZonaService(AppDbContext db, IUsuarioActual usuario)
{
    // La base devuelve el límite como texto: se convierte en la lista de vértices después de leerlo.
    private sealed record ZonaLeida(ZonaDTO Zona, string? Limite)
    {
        public ZonaDTO ConLimite() => Zona with { Limite = LimiteDeZona.Leer(Limite) };
    }

    // Se filtra antes de proyectar: EF no puede filtrar sobre el constructor de un record.
    private IQueryable<ZonaLeida> Proyectar(IQueryable<Zona> zonas)
    {
        // Los clientes que el usuario puede ver: un promotor no ve cuántos clientes tiene la cartera de otro.
        var clientesVisibles = db.Clientes.VisiblesPara(usuario);
        // La misma regla que UsuarioConsultas.ConPermiso, escrita aquí para que la base la resuelva en la misma consulta.
        const string recorrerRuta = Permisos.Territorial.RecorrerRuta;
        return zonas.Select(z => new ZonaLeida(new ZonaDTO(
            z.Id,
            z.Codigo,
            z.Nombre,
            z.Promotor == null ? null : new UsuarioResumenDTO(z.Promotor.Id, z.Promotor.NombreCompleto),
            z.Promotor != null && z.Promotor.Activo && db.UserRoles.Any(ur => ur.UserId == z.PromotorId
                && db.RolesPermisos.Any(rp => rp.RolId == ur.RoleId && rp.Permiso!.Clave == recorrerRuta)),
            clientesVisibles.Count(c => c.ZonaId == z.Id),
            db.PuntosReferencia.Count(p => p.ZonaId == z.Id),
            null), z.Limite));
    }

    public async Task<List<ZonaDTO>> ListarAsync() =>
        (await Proyectar(db.Zonas.OrderBy(z => z.Nombre)).ToListAsync()).Select(z => z.ConLimite()).ToList();

    public async Task<ZonaDTO> ObtenerAsync(int id) =>
        (await Proyectar(db.Zonas.Where(z => z.Id == id)).FirstOrDefaultAsync())?.ConLimite()
        ?? throw new NoEncontradoException("No se encontró la zona.");

    /// <summary>Guarda el límite de la zona (5.5): uno nuevo reemplaza al anterior.</summary>
    public async Task<ZonaDTO> GuardarLimiteAsync(int id, GuardarLimiteDTO dto)
    {
        var zona = await db.Zonas.FindAsync(id) ?? throw new NoEncontradoException("No se encontró la zona.");
        zona.Limite = LimiteDeZona.Escribir(dto.Normalizados());
        await db.SaveChangesAsync();
        return await ObtenerAsync(id);
    }

    public async Task QuitarLimiteAsync(int id)
    {
        var zona = await db.Zonas.FindAsync(id) ?? throw new NoEncontradoException("No se encontró la zona.");
        zona.Limite = null;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// El promotor de una zona recorre rutas y está activo (7.2), sin mirar el nombre de su rol: así lo elige también
    /// PedidoService al asignar pedidos. Las zonas viejas que no cumplen quedan como están hasta que se guardan.
    /// </summary>
    private async Task ExigirPromotorValidoAsync(string promotorId)
    {
        var valido = await db.Users.Where(u => u.Id == promotorId && u.Activo)
            .ConPermiso(db, Permisos.Territorial.RecorrerRuta).AnyAsync();
        if (!valido)
            throw new ValidacionException(new Dictionary<string, string[]>
            {
                ["promotorId"] = ["Elige un promotor activo, que recorra rutas."]
            });
    }

    // La intercalación de la base no distingue mayúsculas ni tildes: "zona norte" choca con "Zona Norte". Los índices
    // únicos frenan además dos registros a la vez.
    private async Task ExigirNombreLibreAsync(string nombre, int? idPropio)
    {
        if (await db.Zonas.AnyAsync(z => z.Nombre == nombre && z.Id != idPropio))
            throw new ConflictoException("zona_nombre_repetido", $"Ya existe la zona \"{nombre}\".");
    }

    public async Task<ZonaDTO> RegistrarAsync(RegistrarZonaDTO dto)
    {
        var codigo = dto.Codigo.Trim();
        var nombre = dto.Nombre.Trim();
        if (await db.Zonas.AnyAsync(z => z.Codigo == codigo))
            throw new ConflictoException("zona_codigo_repetido", $"Ya existe una zona con el código {codigo}.");
        await ExigirNombreLibreAsync(nombre, idPropio: null);
        await ExigirPromotorValidoAsync(dto.PromotorId);
        var zona = new Zona { Codigo = codigo, Nombre = nombre, PromotorId = dto.PromotorId };
        db.Zonas.Add(zona);
        await db.SaveChangesAsync();
        return await ObtenerAsync(zona.Id);
    }

    /// <summary>
    /// Cambia el nombre y el promotor. El cliente siempre tiene el promotor de su zona (7.2): si cambia, sus clientes
    /// pasan al nuevo en la misma transacción. Las HP, cobranzas y pedidos ya registrados conservan el suyo.
    /// </summary>
    public async Task<ZonaDTO> ModificarAsync(int id, ModificarZonaDTO dto)
    {
        var zona = await db.Zonas.FindAsync(id) ?? throw new NoEncontradoException("No se encontró la zona.");
        var nombre = dto.Nombre.Trim();
        await ExigirNombreLibreAsync(nombre, id);
        await ExigirPromotorValidoAsync(dto.PromotorId);
        var cambiaElPromotor = zona.PromotorId != dto.PromotorId;

        await using var transaccion = await db.Database.BeginTransactionAsync();
        zona.Nombre = nombre;
        zona.PromotorId = dto.PromotorId;
        await db.SaveChangesAsync();
        if (cambiaElPromotor)
            await db.Clientes.Where(c => c.ZonaId == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.PromotorId, dto.PromotorId));
        await transaccion.CommitAsync();
        return await ObtenerAsync(id);
    }

    /// <summary>
    /// Una zona con clientes, HP, pedidos o rutas no se elimina (7.2, tesis 7.3.28): el aviso dice qué la usa. Si algo
    /// se le asigna mientras tanto, la FK lo frena igual (409 tiene_registros_asociados).
    /// </summary>
    public async Task EliminarAsync(int id)
    {
        var zona = await db.Zonas.FindAsync(id) ?? throw new NoEncontradoException("No se encontró la zona.");
        string[] usos =
        [
            Cuantos(await db.Clientes.CountAsync(c => c.ZonaId == id), "cliente", "clientes"),
            Cuantos(await db.HojasPedido.CountAsync(h => h.ZonaId == id), "hoja de pedido", "hojas de pedido"),
            Cuantos(await db.Pedidos.CountAsync(p => p.ZonaId == id), "pedido", "pedidos"),
            Cuantos(await db.Rutas.CountAsync(r => r.ZonaId == id), "ruta", "rutas")
        ];
        var enUso = usos.Where(u => u.Length > 0).ToList();
        if (enUso.Count > 0)
            throw new ConflictoException("zona_en_uso", $"La zona tiene {Enumerar(enUso)}: no se puede eliminar.");

        db.Zonas.Remove(zona);
        await db.SaveChangesAsync();
    }

    private static string Cuantos(int cantidad, string singular, string plural) =>
        cantidad switch { 0 => "", 1 => $"1 {singular}", _ => $"{cantidad} {plural}" };

    // "5 clientes, 26 hojas de pedido y 2 rutas".
    private static string Enumerar(List<string> partes) =>
        partes.Count == 1 ? partes[0] : $"{string.Join(", ", partes[..^1])} y {partes[^1]}";
}
