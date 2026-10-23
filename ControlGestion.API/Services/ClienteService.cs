using ControlGestion.API.Configuracion;
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
    private IQueryable<Cliente> Visibles => db.Clientes.VisiblesPara(usuario);

    public async Task<Pagina<ClienteListaDTO>> ListarAsync(FiltrosClienteDTO filtros)
    {
        var consulta = Visibles.Buscar(filtros.Buscar);
        if (filtros.ZonaId.HasValue) consulta = consulta.Where(c => c.ZonaId == filtros.ZonaId);
        if (filtros.EstadoId.HasValue) consulta = consulta.Where(c => c.EstadoClienteId == filtros.EstadoId);
        if (!string.IsNullOrEmpty(filtros.PromotorId)) consulta = consulta.Where(c => c.PromotorId == filtros.PromotorId);
        if (filtros.PorVisitar) consulta = consulta.PorVisitar(HoraBolivia.FinDeHoyUtc(reloj));

        var total = await consulta.CountAsync();
        var items = await consulta
            .OrderBy(c => c.NombreCompleto).ThenBy(c => c.Id)
            .Skip((filtros.Pagina - 1) * filtros.Tamano)
            .Take(filtros.Tamano)
            .Select(ClienteConsultas.ComoLista)
            .ToListAsync();
        return new Pagina<ClienteListaDTO>(items, total, filtros.Pagina, filtros.Tamano);
    }

    /// <summary>
    /// Para los selectores de HP, pedidos, eventos y rutas: los clientes visibles que no están inactivos, por nombre. Con
    /// límite, solo los primeros: el selector con búsqueda pide de a pocos mientras se escribe. La app pide también los
    /// inactivos, para mostrarlos deshabilitados con su estado (7.3.5, ext. 2.2c) en vez de que parezca que no existen.
    /// </summary>
    public Task<List<ClienteOpcionDTO>> OpcionesAsync(string? buscar, int? limite = null, bool incluirInactivos = false)
    {
        var visibles = incluirInactivos ? Visibles : Visibles.Where(c => c.EstadoClienteId != EstadosDeCliente.Inactivo);
        var consulta = visibles.Buscar(buscar).OrderBy(c => c.NombreCompleto).ThenBy(c => c.Id);
        return (limite is { } n ? consulta.Take(n) : consulta).Select(ClienteConsultas.ComoOpcion).ToListAsync();
    }

    /// <summary>
    /// El mapa de clientes (5.1 y 5.2): todos los de la cartera con ubicación, también los de un estado asignado a mano,
    /// que se ven con su color (7.3.18, ext. *c). Un cliente sin ubicación (los viejos, con 0,0) no aparece (7.3.17, ext. 2.4b).
    /// </summary>
    public Task<List<ClienteMapaDTO>> MapaAsync(int? zonaId)
    {
        var consulta = Visibles.Where(c => c.Latitud != 0 && c.Longitud != 0);
        if (zonaId.HasValue) consulta = consulta.Where(c => c.ZonaId == zonaId);
        return consulta.OrderBy(c => c.NombreCompleto).ThenBy(c => c.Id).Select(ClienteConsultas.ComoMapa).ToListAsync();
    }

    /// <summary>
    /// La lista de clientes por visitar (5.3, 7.3.19): primero los nunca visitados y después del más atrasado al más
    /// reciente, con los días de atraso de hoy.
    /// </summary>
    public async Task<Pagina<ClientePorVisitarDTO>> PorVisitarAsync(FiltrosPorVisitarDTO filtros)
    {
        var consulta = Visibles.Buscar(filtros.Buscar).PorVisitar(HoraBolivia.FinDeHoyUtc(reloj));
        if (filtros.ZonaId.HasValue) consulta = consulta.Where(c => c.ZonaId == filtros.ZonaId);
        if (!string.IsNullOrEmpty(filtros.PromotorId)) consulta = consulta.Where(c => c.PromotorId == filtros.PromotorId);

        var total = await consulta.CountAsync();
        var filas = await consulta.DelMasAtrasado()
            .Skip((filtros.Pagina - 1) * filtros.Tamano)
            .Take(filtros.Tamano)
            .Select(c => new ClientePorVisitarDTO(c.Id, c.Codigo, c.NombreCompleto, c.DireccionTexto, c.Latitud, c.Longitud,
                new ZonaResumenDTO(c.Zona!.Id, c.Zona.Nombre),
                c.Promotor == null ? null : new UsuarioResumenDTO(c.Promotor.Id, c.Promotor.NombreCompleto),
                c.UltimaVisita, c.ProximaVisita, null))
            .ToListAsync();
        var hoy = HoraBolivia.Hoy(reloj);
        var items = filas.Select(c => c with { DiasDeAtraso = ClienteConsultas.DiasDeAtraso(c.ProximaVisita, hoy) }).ToList();
        return new Pagina<ClientePorVisitarDTO>(items, total, filtros.Pagina, filtros.Tamano);
    }

    public async Task<ClienteDTO> ObtenerAsync(int id) =>
        await Visibles.Where(c => c.Id == id).Select(ClienteConsultas.ComoDetalle).FirstOrDefaultAsync()
        ?? throw new NoEncontradoException("No se encontró el cliente.");

    public Task<List<EstadoClienteDTO>> EstadosAsync() =>
        db.EstadosCliente.OrderBy(e => e.Id).Select(ClienteConsultas.EstadoComoDTO).ToListAsync();

    public async Task<ClienteDTO> CrearAsync(GuardarClienteDTO dto)
    {
        // Antes del aviso del celular: el reintento de un cliente ya registrado repite su propio celular.
        if (await YaRegistradoAsync(dto.ClaveIdempotencia) is { } repetido)
            return repetido;

        var promotorDeLaZona = await PromotorDeLaZonaAsync(dto.ZonaId);
        await AvisarSiElTelefonoSeRepiteAsync(dto, idPropio: null);
        var cliente = new Cliente
        {
            Codigo = await numerador.SiguienteAsync(NumeradorService.Cliente),
            // Un cliente sin visitas está disponible para re-visita (5.2).
            EstadoClienteId = EstadosDeCliente.DisponibleParaRevisita,
            CreadoPorId = usuario.Id,
            FechaCreacion = reloj.GetUtcNow().UtcDateTime,
            ClaveIdempotencia = dto.ClaveIdempotencia
        };
        Copiar(dto, cliente, promotorDeLaZona);
        db.Clientes.Add(cliente);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException) when (dto.ClaveIdempotencia != null)
        {
            // Dos envíos del mismo cliente a la vez: el índice único frena al segundo, que devuelve el del primero.
            db.ChangeTracker.Clear();
            if (await YaRegistradoAsync(dto.ClaveIdempotencia) is { } ganador) return ganador;
            throw;
        }
        return await ObtenerAsync(cliente.Id);
    }

    /// <summary>
    /// El cliente que ya se registró con esa clave, o null si la clave es nueva. La clave se guarda solo con un cliente
    /// guardado: un registro rechazado (un dato inválido, el celular repetido sin confirmar) no la gasta.
    /// </summary>
    private async Task<ClienteDTO?> YaRegistradoAsync(Guid? clave)
    {
        if (clave == null) return null;
        var id = await db.Clientes.Where(c => c.ClaveIdempotencia == clave).Select(c => (int?)c.Id).FirstOrDefaultAsync();
        if (id == null) return null;
        return await Visibles.Where(c => c.Id == id).Select(ClienteConsultas.ComoDetalle).FirstOrDefaultAsync()
            ?? throw new ConflictoException("clave_idempotencia_usada", "Esa clave de idempotencia ya se usó en otro cliente.");
    }

    public async Task<ClienteDTO> EditarAsync(int id, GuardarClienteDTO dto)
    {
        var cliente = await Visibles.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NoEncontradoException("No se encontró el cliente.");
        // Solo se avisa si el celular cambió: si no, editar otro dato avisaría cada vez.
        if (cliente.Telefono != dto.Telefono.Trim())
            await AvisarSiElTelefonoSeRepiteAsync(dto, idPropio: id);
        // Editar no toca el estado: antes lo devolvía a "Activo" sin avisar.
        Copiar(dto, cliente, await PromotorDeLaZonaAsync(dto.ZonaId));
        await db.SaveChangesAsync();
        return await ObtenerAsync(id);
    }

    /// <summary>Desactivar a un cliente es asignarle el estado Inactivo: los clientes no se borran.</summary>
    public async Task<ClienteDTO> AsignarEstadoAsync(int id, AsignarEstadoClienteDTO dto)
    {
        var cliente = await Visibles.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NoEncontradoException("No se encontró el cliente.");

        if (dto.EstadoClienteId is { } estadoId)
        {
            var estado = await db.EstadosCliente.Where(e => e.Id == estadoId).Select(e => new { e.EsDelSistema }).FirstOrDefaultAsync()
                ?? throw new ReglaDeNegocioException("estado_no_existe", "El estado elegido no existe.");
            if (estado.EsDelSistema)
                throw new ReglaDeNegocioException("estado_del_sistema",
                    "Activo y Disponible para re-visita los asigna el sistema según las visitas.");
            cliente.EstadoClienteId = estadoId;
        }
        else
        {
            // La misma regla que la lista "por visitar" y el recálculo de los días de re-visita.
            var llego = await db.Clientes.Where(c => c.Id == id)
                .Where(ClienteConsultas.ProximaVisitaLlego(HoraBolivia.FinDeHoyUtc(reloj))).AnyAsync();
            cliente.EstadoClienteId = llego ? EstadosDeCliente.DisponibleParaRevisita : EstadosDeCliente.Activo;
        }

        await db.SaveChangesAsync();
        return await ObtenerAsync(id);
    }

    /// <summary>
    /// Avisar si el celular ya existe (2.2): puede ser el mismo cliente registrado dos veces. No se bloquea, porque un
    /// celular también se comparte (un negocio familiar), pero quien registra tiene que confirmarlo.
    /// </summary>
    private async Task AvisarSiElTelefonoSeRepiteAsync(GuardarClienteDTO dto, int? idPropio)
    {
        if (dto.ConfirmarTelefonoRepetido) return;
        var telefono = dto.Telefono.Trim();
        var otro = await db.Clientes.Where(c => c.Telefono == telefono && c.Id != idPropio).Select(c => new { c.Id }).FirstOrDefaultAsync();
        if (otro == null) return;

        // Solo se nombra al otro cliente si el usuario puede verlo: a un promotor no se le muestra la cartera de otro.
        var visible = await Visibles.Where(c => c.Id == otro.Id).Select(c => new { c.NombreCompleto, c.Codigo }).FirstOrDefaultAsync();
        throw new ConflictoException("telefono_repetido", visible == null
            ? "Ya existe otro cliente con ese celular."
            : $"Ya existe el cliente {visible.NombreCompleto} ({visible.Codigo}) con ese celular.");
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
    }
}
