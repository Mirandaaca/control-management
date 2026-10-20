using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>Clientes en campo (2.2) y su ubicación (2.4). Cada usuario ve y edita solo su cartera, salvo con cartera completa.</summary>
public class ClienteService(AppDbContext db, IUsuarioActual usuario, NumeradorService numerador, TimeProvider reloj)
{
    private IQueryable<Cliente> Visibles => db.Clientes.Where(c => c.Activo).VisiblesPara(usuario);

    public async Task<Pagina<ClienteListaDTO>> ListarAsync(FiltrosClienteDTO filtros)
    {
        var consulta = Visibles.Buscar(filtros.Buscar);
        if (filtros.ZonaId.HasValue) consulta = consulta.Where(c => c.ZonaId == filtros.ZonaId);
        if (filtros.EstadoId.HasValue) consulta = consulta.Where(c => c.EstadoClienteId == filtros.EstadoId);
        if (!string.IsNullOrEmpty(filtros.PromotorId)) consulta = consulta.Where(c => c.PromotorId == filtros.PromotorId);
        if (filtros.PorVisitar)
        {
            // Un cliente nunca visitado no tiene próxima visita, y es el primero que hay que visitar. Los estados
            // asignados a mano (Inactivo y otros) sacan al cliente del ciclo de visitas.
            var ahora = reloj.GetUtcNow().UtcDateTime;
            consulta = consulta.Where(c => EstadosDeCliente.DelSistema.Contains(c.EstadoClienteId)
                && (c.ProximaVisita == null || c.ProximaVisita <= ahora));
        }

        var total = await consulta.CountAsync();
        var items = await consulta
            .OrderBy(c => c.NombreCompleto).ThenBy(c => c.Id)
            .Skip((filtros.Pagina - 1) * filtros.Tamano)
            .Take(filtros.Tamano)
            .Select(ClienteConsultas.ComoLista)
            .ToListAsync();
        return new Pagina<ClienteListaDTO>(items, total, filtros.Pagina, filtros.Tamano);
    }

    /// <summary>Para los selectores de HP, pedidos, eventos y rutas: todos los clientes visibles, con lo justo para elegir.</summary>
    public Task<List<ClienteOpcionDTO>> OpcionesAsync(string? buscar) =>
        Visibles.Buscar(buscar).OrderBy(c => c.NombreCompleto).Select(ClienteConsultas.ComoOpcion).ToListAsync();

    public Task<List<ClienteMapaDTO>> MapaAsync(int? zonaId)
    {
        var consulta = Visibles.Where(c => c.Latitud != 0 && c.Longitud != 0);
        if (zonaId.HasValue) consulta = consulta.Where(c => c.ZonaId == zonaId);
        return consulta.Select(ClienteConsultas.ComoMapa).ToListAsync();
    }

    public async Task<ClienteDTO> ObtenerAsync(int id) =>
        await Visibles.Where(c => c.Id == id).Select(ClienteConsultas.ComoDetalle).FirstOrDefaultAsync()
        ?? throw new NoEncontradoException("No se encontró el cliente.");

    public Task<List<EstadoClienteDTO>> EstadosAsync() =>
        db.EstadosCliente.Where(e => e.Activo).OrderBy(e => e.Id).Select(ClienteConsultas.EstadoComoDTO).ToListAsync();

    public async Task<ClienteDTO> CrearAsync(GuardarClienteDTO dto)
    {
        var promotorDeLaZona = await PromotorDeLaZonaAsync(dto.ZonaId);
        var cliente = new Cliente
        {
            Codigo = await numerador.SiguienteAsync(NumeradorService.Cliente),
            // Un cliente sin visitas está disponible para re-visita (5.2).
            EstadoClienteId = EstadosDeCliente.DisponibleParaRevisita,
            CreadoPorId = usuario.Id,
            FechaCreacion = reloj.GetUtcNow().UtcDateTime
        };
        Copiar(dto, cliente, promotorDeLaZona);
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();
        return await ObtenerAsync(cliente.Id);
    }

    public async Task<ClienteDTO> EditarAsync(int id, GuardarClienteDTO dto)
    {
        var cliente = await Visibles.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NoEncontradoException("No se encontró el cliente.");
        Copiar(dto, cliente, await PromotorDeLaZonaAsync(dto.ZonaId));
        cliente.EstadoClienteId = dto.EstadoClienteId;
        await db.SaveChangesAsync();
        return await ObtenerAsync(id);
    }

    public async Task DesactivarAsync(int id)
    {
        var cliente = await Visibles.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NoEncontradoException("No se encontró el cliente.");
        cliente.Activo = false;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// El cliente siempre tiene el promotor de su zona (7.2). Sin cartera completa, el usuario solo registra o mueve
    /// clientes a sus propias zonas: en otra, el cliente pasaría a otro promotor y desaparecería de su lista.
    /// </summary>
    private async Task<string?> PromotorDeLaZonaAsync(int zonaId)
    {
        var zona = await db.Zonas.Where(z => z.Id == zonaId).Select(z => new { z.PromotorId }).FirstOrDefaultAsync()
            ?? throw new ReglaDeNegocioException("zona_no_existe", "La zona elegida no existe.");
        if (!usuario.VeCarteraCompleta && zona.PromotorId != usuario.Id)
            throw new ReglaDeNegocioException("zona_fuera_de_cartera", "Solo puedes registrar clientes en tus zonas.");
        return zona.PromotorId;
    }

    private static void Copiar(GuardarClienteDTO dto, Cliente cliente, string? promotorDeLaZona)
    {
        cliente.NombreCompleto = dto.NombreCompleto.Trim();
        cliente.RazonSocial = dto.RazonSocial?.Trim();
        cliente.NIT = dto.NIT?.Trim();
        cliente.Telefono = dto.Telefono.Trim();
        cliente.Email = dto.Email?.Trim();
        cliente.DireccionTexto = dto.DireccionTexto.Trim();
        cliente.Latitud = dto.Latitud!.Value;
        cliente.Longitud = dto.Longitud!.Value;
        cliente.ZonaId = dto.ZonaId;
        cliente.PromotorId = promotorDeLaZona;
        cliente.DiasRevisita = dto.DiasRevisita;
        cliente.TieneCredito = dto.TieneCredito;
        cliente.LimiteCredito = dto.LimiteCredito;
    }
}
