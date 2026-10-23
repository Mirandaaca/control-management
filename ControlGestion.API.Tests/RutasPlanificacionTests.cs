using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>Planificar una ruta y la ruta sugerida (7.3.20, pasos 1 y 2).</summary>
[Collection(ColeccionApi.Nombre)]
public class RutasPlanificacionTests(ApiDePrueba api)
{
    private static readonly DateOnly Hoy = HoraBolivia.Hoy(TimeProvider.System);
    private static string Dia(DateOnly dia) => dia.ToString("yyyy-MM-dd");

    /// <summary>Un cliente de la zona en ese punto, con su próxima visita ese día a media tarde (sin día, nunca visitado).</summary>
    private async Task<int> ClienteAsync(HttpClient operador, int zonaId, DateOnly? proxima, decimal latitud = -17.78m,
        decimal longitud = -63.18m, int estadoId = EstadosDeCliente.Activo)
    {
        var id = await operador.CrearClienteAsync(zonaId);
        DateTime? proximaUtc = proxima is { } dia ? HoraBolivia.InicioDelDiaUtc(dia).AddHours(15) : null;
        using var scope = api.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Clientes.Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Latitud, latitud)
                .SetProperty(c => c.Longitud, longitud)
                .SetProperty(c => c.UltimaVisita, proximaUtc == null ? null : proximaUtc.Value.AddDays(-15))
                .SetProperty(c => c.ProximaVisita, proximaUtc)
                .SetProperty(c => c.EstadoClienteId, proxima == null ? EstadosDeCliente.DisponibleParaRevisita : estadoId));
        return id;
    }

    private static Task<HttpResponseMessage> PlanificarAsync(HttpClient gerente, int zonaId, string promotorId, DateOnly fecha,
        params int[] clientes) =>
        gerente.PostAsJsonAsync("/api/rutas", new { zonaId, promotorId, fecha = Dia(fecha), clientes });

    private static List<int> Ids(JsonElement lista) => lista.EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).ToList();

    [Fact]
    public async Task El_gerente_planifica_la_ruta_para_el_promotor_y_el_promotor_la_ve()
    {
        var gerente = await api.CrearUsuarioAsync(Roles.GerenteGeneral);
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var otro = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync(promotor.Id);
        var a = await ClienteAsync(operador, zonaId, null);
        var b = await ClienteAsync(operador, zonaId, Hoy.AddDays(-3));
        using var comoGerente = api.ClienteCon(gerente.Token);

        var respuesta = await PlanificarAsync(comoGerente, zonaId, promotor.Id, Hoy.AddDays(1), b, a);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var ruta = await respuesta.LeerJsonAsync();
        var id = ruta.GetProperty("id").GetInt32();
        Assert.StartsWith("RUT-", ruta.GetProperty("numero").GetString());
        Assert.Equal(promotor.Id, ruta.GetProperty("promotor").GetProperty("id").GetString());
        Assert.Equal(Dia(Hoy.AddDays(1)), ruta.GetProperty("fecha").GetString());
        Assert.Equal((int)EstadoRuta.Planificada, ruta.GetProperty("estado").GetInt32());
        var visitas = ruta.GetProperty("visitas").EnumerateArray().ToList();
        Assert.Equal([b, a], visitas.Select(v => v.GetProperty("cliente").GetProperty("id").GetInt32()));
        Assert.Equal([1, 2], visitas.Select(v => v.GetProperty("orden").GetInt32()));
        Assert.Equal(2, ruta.GetProperty("pendientes").GetInt32());

        using var comoPromotor = api.ClienteCon(promotor.Token);
        Assert.Equal([id], Ids(await (await comoPromotor.GetAsync("/api/rutas")).LeerJsonAsync()));
        using var comoOtro = api.ClienteCon(otro.Token);
        Assert.Empty(Ids(await (await comoOtro.GetAsync("/api/rutas")).LeerJsonAsync()));
        await (await comoOtro.GetAsync($"/api/rutas/{id}")).ProblemaAsync(404);
    }

    [Fact]
    public async Task Una_fecha_pasada_o_sin_clientes_no_se_registra_y_dice_que_corregir()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync(promotor.Id);
        var cliente = await ClienteAsync(operador, zonaId, null);

        var pasada = await (await PlanificarAsync(operador, zonaId, promotor.Id, Hoy.AddDays(-1), cliente)).ProblemaAsync(400);
        var vacia = await (await PlanificarAsync(operador, zonaId, promotor.Id, Hoy)).ProblemaAsync(400);

        Assert.Equal("La fecha ya pasó.", pasada.GetProperty("errors").GetProperty("fecha")[0].GetString());
        Assert.Equal("Agrega al menos un cliente a la ruta.", vacia.GetProperty("errors").GetProperty("clientes")[0].GetString());
        Assert.Empty(Ids(await (await operador.GetAsync($"/api/rutas?promotorId={promotor.Id}")).LeerJsonAsync()));
    }

    [Fact]
    public async Task Hoy_todavia_se_puede_planificar()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync(promotor.Id);

        var respuesta = await PlanificarAsync(operador, zonaId, promotor.Id, Hoy, await ClienteAsync(operador, zonaId, null));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_promotor_no_tiene_dos_rutas_el_mismo_dia()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync(promotor.Id);
        var cliente = await ClienteAsync(operador, zonaId, null);
        var primera = await (await PlanificarAsync(operador, zonaId, promotor.Id, Hoy.AddDays(2), cliente)).LeerJsonAsync();

        var problema = await (await PlanificarAsync(operador, zonaId, promotor.Id, Hoy.AddDays(2), cliente))
            .ProblemaAsync(409, "ruta_repetida");

        Assert.Equal($"Prueba {Roles.Promotor} ya tiene una ruta para el {Hoy.AddDays(2):dd/MM/yyyy} ({primera.GetProperty("numero").GetString()}).",
            problema.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Los_clientes_son_de_la_zona_sin_repetirse_y_el_promotor_recorre_rutas()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync(promotor.Id);
        var suyo = await ClienteAsync(operador, zonaId, null);
        var deOtraZona = await ClienteAsync(operador, await operador.CrearZonaAsync(), null);

        var otraZona = await (await PlanificarAsync(operador, zonaId, promotor.Id, Hoy, suyo, deOtraZona)).ProblemaAsync(400);
        var repetido = await (await PlanificarAsync(operador, zonaId, promotor.Id, Hoy, suyo, suyo)).ProblemaAsync(400);
        var noPromotor = await (await PlanificarAsync(operador, zonaId, secretaria.Id, Hoy, suyo)).ProblemaAsync(400);

        Assert.EndsWith("no es de " + (await (await operador.GetAsync($"/api/zonas/{zonaId}")).LeerJsonAsync()).GetProperty("nombre").GetString() + ".",
            otraZona.GetProperty("errors").GetProperty("clientes")[0].GetString());
        Assert.Equal("Un cliente no puede ir dos veces en la ruta.", repetido.GetProperty("errors").GetProperty("clientes")[0].GetString());
        Assert.True(noPromotor.GetProperty("errors").TryGetProperty("promotorId", out _));
    }

    [Fact]
    public async Task La_secretaria_no_planifica_rutas()
    {
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        using var cliente = api.ClienteCon(secretaria.Token);

        await (await PlanificarAsync(cliente, 1, secretaria.Id, Hoy, 1)).ProblemaAsync(403);
        await (await cliente.GetAsync("/api/rutas")).ProblemaAsync(403);
    }

    // Extensión nueva de la 7.3.20 (tarea 14): para corregir una ruta se elimina y se planifica otra para ese día.
    [Fact]
    public async Task Una_ruta_planificada_se_elimina_con_sus_visitas_y_se_planifica_otra_para_ese_dia()
    {
        var gerente = await api.CrearUsuarioAsync(Roles.GerenteGeneral);
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync(promotor.Id);
        var cliente = await ClienteAsync(operador, zonaId, null);
        using var comoGerente = api.ClienteCon(gerente.Token);
        var ruta = await (await PlanificarAsync(comoGerente, zonaId, promotor.Id, Hoy.AddDays(3), cliente)).LeerJsonAsync();
        var id = ruta.GetProperty("id").GetInt32();
        var zona = (await (await operador.GetAsync($"/api/zonas/{zonaId}")).LeerJsonAsync()).GetProperty("nombre").GetString();
        Assert.Equal($"{zona} · {Hoy.AddDays(3):dd/MM/yyyy}", ruta.GetProperty("nombre").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await comoGerente.DeleteAsync($"/api/rutas/{id}")).StatusCode);

        await (await comoGerente.GetAsync($"/api/rutas/{id}")).ProblemaAsync(404);
        using (var scope = api.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().RutasDetalle.AnyAsync(d => d.RutaId == id));
        Assert.Equal(HttpStatusCode.Created, (await PlanificarAsync(comoGerente, zonaId, promotor.Id, Hoy.AddDays(3), cliente)).StatusCode);
    }

    [Fact]
    public async Task Una_ruta_en_curso_o_completada_no_se_elimina()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync(promotor.Id);
        var ruta = await (await PlanificarAsync(operador, zonaId, promotor.Id, Hoy, await ClienteAsync(operador, zonaId, null)))
            .LeerJsonAsync();
        var id = ruta.GetProperty("id").GetInt32();
        using var comoPromotor = api.ClienteCon(promotor.Token);

        // El promotor no planifica: tampoco elimina.
        await (await comoPromotor.DeleteAsync($"/api/rutas/{id}")).ProblemaAsync(403);
        (await comoPromotor.PostAsync($"/api/rutas/{id}/iniciar", null)).EnsureSuccessStatusCode();
        var enCurso = await (await operador.DeleteAsync($"/api/rutas/{id}")).ProblemaAsync(400, "ruta_ya_iniciada");
        (await comoPromotor.PostAsync($"/api/rutas/{id}/terminar", null)).EnsureSuccessStatusCode();
        var completada = await (await operador.DeleteAsync($"/api/rutas/{id}")).ProblemaAsync(400, "ruta_ya_iniciada");

        Assert.Equal("La ruta ya está en curso: no se puede eliminar.", enCurso.GetProperty("detail").GetString());
        Assert.Equal("La ruta ya se completó: no se puede eliminar.", completada.GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.OK, (await operador.GetAsync($"/api/rutas/{id}")).StatusCode);
    }

    // 7.3.20, paso 2.2: primero el más atrasado (el nunca visitado) y después, cada vez, el más cercano al anterior.
    [Fact]
    public async Task La_sugerida_empieza_por_el_mas_atrasado_y_sigue_por_el_mas_cercano()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var lejosAtrasado = await ClienteAsync(operador, zonaId, Hoy.AddDays(-10), -17.80m, -63.20m);
        var nunca = await ClienteAsync(operador, zonaId, null, -17.70m, -63.10m);
        var cercaDelPrimero = await ClienteAsync(operador, zonaId, Hoy.AddDays(-2), -17.71m, -63.11m);
        await ClienteAsync(operador, zonaId, Hoy.AddDays(5), -17.70m, -63.10m);
        await ClienteAsync(operador, zonaId, Hoy.AddDays(-20), -17.70m, -63.10m, EstadosDeCliente.Inactivo);

        var sugerida = await (await operador.GetAsync($"/api/rutas/sugerida?zonaId={zonaId}&fecha={Dia(Hoy)}")).LeerJsonAsync();

        Assert.Equal([nunca, cercaDelPrimero, lejosAtrasado], Ids(sugerida));
        Assert.Equal([JsonValueKind.Null, JsonValueKind.Number, JsonValueKind.Number],
            sugerida.EnumerateArray().Select(c => c.GetProperty("diasDeAtraso").ValueKind));
        Assert.Equal(10, sugerida[2].GetProperty("diasDeAtraso").GetInt32());
    }

    // La re-visita vencida se cuenta al día de la ruta: el cliente que toca visitar mañana entra en la ruta de mañana.
    [Fact]
    public async Task La_sugerida_cuenta_la_re_visita_vencida_al_dia_de_la_ruta()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var manana = await ClienteAsync(operador, zonaId, Hoy.AddDays(1));

        var paraHoy = await (await operador.GetAsync($"/api/rutas/sugerida?zonaId={zonaId}&fecha={Dia(Hoy)}")).LeerJsonAsync();
        var paraPasado = await (await operador.GetAsync($"/api/rutas/sugerida?zonaId={zonaId}&fecha={Dia(Hoy.AddDays(2))}")).LeerJsonAsync();

        Assert.Empty(Ids(paraHoy));
        Assert.Equal([manana], Ids(paraPasado));
        Assert.Equal(1, paraPasado[0].GetProperty("diasDeAtraso").GetInt32());
    }
}
