using System.Text.Json;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>La lista de clientes por visitar (5.3, 7.3.19) y el mapa de clientes (5.1 y 5.2, 7.3.17 y 7.3.18).</summary>
[Collection(ColeccionApi.Nombre)]
public class ClientesPorVisitarTests(ApiDePrueba api)
{
    private static readonly DateOnly Hoy = HoraBolivia.Hoy(TimeProvider.System);

    /// <summary>Un cliente cuya próxima visita fue (o será) ese día de Bolivia, a media tarde; sin día, nunca visitado.</summary>
    private async Task<int> ClienteAsync(HttpClient operador, int zonaId, DateOnly? proxima, int estadoId = EstadosDeCliente.Activo)
    {
        var id = await operador.CrearClienteAsync(zonaId);
        if (proxima is not { } dia) return id;
        var proximaUtc = HoraBolivia.InicioDelDiaUtc(dia).AddHours(15);
        using var scope = api.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Clientes.Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.UltimaVisita, proximaUtc.AddDays(-15))
                .SetProperty(c => c.ProximaVisita, proximaUtc)
                .SetProperty(c => c.EstadoClienteId, estadoId));
        return id;
    }

    private static async Task<List<JsonElement>> PorVisitarAsync(HttpClient cliente, string filtros)
    {
        var respuesta = await cliente.GetAsync($"/api/clientes/por-visitar{filtros}");
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.LeerJsonAsync()).GetProperty("items").EnumerateArray().ToList();
    }

    private static int? Atraso(JsonElement c) =>
        c.GetProperty("diasDeAtraso").ValueKind == JsonValueKind.Null ? null : c.GetProperty("diasDeAtraso").GetInt32();

    [Fact]
    public async Task Van_primero_los_nunca_visitados_y_despues_del_mas_atrasado_al_mas_reciente()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var hace1 = await ClienteAsync(operador, zonaId, Hoy.AddDays(-1));
        var nunca = await ClienteAsync(operador, zonaId, null);
        var hoy = await ClienteAsync(operador, zonaId, Hoy);
        var hace5 = await ClienteAsync(operador, zonaId, Hoy.AddDays(-5), EstadosDeCliente.DisponibleParaRevisita);
        await ClienteAsync(operador, zonaId, Hoy.AddDays(1));
        await ClienteAsync(operador, zonaId, Hoy.AddDays(-9), EstadosDeCliente.Inactivo);

        var lista = await PorVisitarAsync(operador, $"?zonaId={zonaId}");

        Assert.Equal([nunca, hace5, hace1, hoy], lista.Select(c => c.GetProperty("id").GetInt32()));
        Assert.Equal([null, 5, 1, 0], lista.Select(Atraso));
        Assert.Equal(JsonValueKind.Null, lista[0].GetProperty("ultimaVisita").ValueKind);
    }

    [Fact]
    public async Task El_promotor_ve_solo_los_suyos_y_el_gerente_filtra_por_promotor()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var gerente = await api.CrearUsuarioAsync(Roles.GerenteGeneral);
        using var operador = await api.OperadorAsync();
        var suyo = await ClienteAsync(operador, await operador.CrearZonaAsync(promotor.Id), Hoy.AddDays(-2));
        var ajeno = await ClienteAsync(operador, await operador.CrearZonaAsync(), Hoy.AddDays(-2));
        using var comoPromotor = api.ClienteCon(promotor.Token);
        using var comoGerente = api.ClienteCon(gerente.Token);

        var delPromotor = (await PorVisitarAsync(comoPromotor, "?tamano=100")).Select(c => c.GetProperty("id").GetInt32()).ToList();
        var filtrados = (await PorVisitarAsync(comoGerente, $"?promotorId={promotor.Id}&tamano=100"))
            .Select(c => c.GetProperty("id").GetInt32()).ToList();

        Assert.Equal([suyo], delPromotor);
        Assert.Equal([suyo], filtrados);
        Assert.DoesNotContain(ajeno, filtrados);
    }

    [Fact]
    public async Task La_secretaria_no_tiene_la_lista_por_visitar()
    {
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        using var cliente = api.ClienteCon(secretaria.Token);

        await (await cliente.GetAsync("/api/clientes/por-visitar")).ProblemaAsync(403);
    }

    // Quien planifica una ruta elige sus clientes del mapa de la zona (el formulario de Rutas).
    [Fact]
    public async Task Quien_planifica_rutas_puede_pedir_el_mapa_de_clientes()
    {
        using var admin = await api.AdminAsync();
        var (_, rol) = await admin.CrearRolAsync(Permisos.Territorial.PlanificarRuta, Permisos.Seguridad.VerCarteraCompleta);
        var planificador = await api.CrearUsuarioAsync(rol);
        using var cliente = api.ClienteCon(planificador.Token);

        Assert.True((await cliente.GetAsync("/api/clientes/mapa")).IsSuccessStatusCode);
    }

    // 7.3.18, ext. *c: un estado asignado a mano se ve en el mapa con su color, no se esconde.
    [Fact]
    public async Task El_mapa_muestra_tambien_a_los_inactivos_y_la_ultima_visita()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var inactivo = await ClienteAsync(operador, zonaId, Hoy.AddDays(3), EstadosDeCliente.Inactivo);

        var mapa = (await (await operador.GetAsync($"/api/clientes/mapa?zonaId={zonaId}")).LeerJsonAsync()).EnumerateArray().ToList();

        var cliente = Assert.Single(mapa);
        Assert.Equal(inactivo, cliente.GetProperty("id").GetInt32());
        Assert.Equal("Inactivo", cliente.GetProperty("estado").GetProperty("nombre").GetString());
        Assert.Equal(zonaId, cliente.GetProperty("zonaId").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, cliente.GetProperty("ultimaVisita").ValueKind);
    }
}
