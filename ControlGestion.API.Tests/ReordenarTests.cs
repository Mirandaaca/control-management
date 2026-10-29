using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ControlGestion.API.Tests;

/// <summary>
/// Reordenar lo que le falta al promotor desde donde está (parte C de la tarea 25), con un OpenRouteService falso que
/// visita las paradas al revés. El orden sugerido tiene un límite de uno cada 20 s por usuario: cada prueba pide uno solo.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class ReordenarTests(ApiDePrueba api)
{
    private static readonly DateOnly Hoy = HoraBolivia.Hoy(TimeProvider.System);
    private const string DesdeElOrigen = "latitud=-17.78&longitud=-63.19";
    private readonly string _clave = $"clave-de-prueba-{Guid.NewGuid():N}";

    private sealed record Recorrido(UsuarioDePrueba Promotor, HttpClient ComoPromotor, int RutaId, int[] Visitas)
        : IDisposable
    {
        public void Dispose() => ComoPromotor.Dispose();
    }

    /// <summary>Un promotor con una ruta de hoy, con un cliente nuevo en cada punto (de norte a sur); en curso, salvo que se pida lo contrario.</summary>
    private async Task<Recorrido> RutaAsync(int clientes = 3, bool enCurso = true)
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync(promotor.Id);
        var ids = new int[clientes];
        for (var i = 0; i < clientes; i++) ids[i] = await operador.CrearClienteAsync(zonaId);
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < clientes; i++)
                await db.Clientes.Where(c => c.Id == ids[i]).ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Latitud, -17.77m - i * 0.002m)
                    .SetProperty(c => c.Longitud, -63.18m + i * 0.002m));
        }
        var respuesta = await operador.PostAsJsonAsync("/api/rutas",
            new { zonaId, promotorId = promotor.Id, fecha = Hoy.ToString("yyyy-MM-dd"), clientes = ids });
        respuesta.EnsureSuccessStatusCode();
        var ruta = await respuesta.LeerJsonAsync();
        var rutaId = ruta.GetProperty("id").GetInt32();
        var comoPromotor = api.ClienteCon(promotor.Token);
        if (enCurso) (await comoPromotor.PostAsync($"/api/rutas/{rutaId}/iniciar", null)).EnsureSuccessStatusCode();
        var visitas = ruta.GetProperty("visitas").EnumerateArray().Select(v => v.GetProperty("id").GetInt32()).ToArray();
        return new Recorrido(promotor, comoPromotor, rutaId, visitas);
    }

    private static async Task MarcarAsync(Recorrido r, int visitaId, EstadoVisita resultado, string? observacion = null) =>
        (await r.ComoPromotor.PostAsJsonAsync($"/api/rutas/{r.RutaId}/visitas/{visitaId}",
            new { resultado = (int)resultado, latitud = -17.7834m, longitud = -63.1822m, observacion })).EnsureSuccessStatusCode();

    private static async Task<HttpResponseMessage> OrdenAsync(WebApplicationFactory<Program> fabrica, Recorrido r,
        string consulta = DesdeElOrigen)
    {
        using var cliente = fabrica.ClienteCon(r.Promotor.Token);
        return await cliente.GetAsync($"/api/rutas/{r.RutaId}/orden-sugerido?{consulta}");
    }

    private static Task<HttpResponseMessage> ReordenarAsync(Recorrido r, int[] visitas, int[]? vuelven = null) =>
        r.ComoPromotor.PostAsJsonAsync($"/api/rutas/{r.RutaId}/reordenar",
            new { visitas, ausentesQueVuelven = vuelven ?? [] });

    private static (int Id, int Orden, int Resultado)[] Visitas(JsonElement ruta) =>
        [.. ruta.GetProperty("visitas").EnumerateArray()
            .Select(v => (v.GetProperty("id").GetInt32(), v.GetProperty("orden").GetInt32(), v.GetProperty("resultado").GetInt32()))
            .OrderBy(v => v.Item2)];

    [Fact]
    public async Task El_orden_sugerido_ordena_las_pendientes_desde_donde_esta_el_promotor_sin_guardar_nada()
    {
        var proveedor = new OpenRouteServiceFalso();
        await using var fabrica = api.ConNavegacion(proveedor, _clave);
        using var r = await RutaAsync();

        var respuesta = await OrdenAsync(fabrica, r, $"{DesdeElOrigen}&medio=pie");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var orden = await respuesta.LeerJsonAsync();
        Assert.Equal(new[] { (r.Visitas[2], 1), (r.Visitas[1], 2), (r.Visitas[0], 3) },
            orden.GetProperty("visitas").EnumerateArray().Select(v => (v.GetProperty("id").GetInt32(), v.GetProperty("orden").GetInt32())));
        Assert.All(orden.GetProperty("visitas").EnumerateArray(), v => Assert.False(v.GetProperty("ausente").GetBoolean()));
        Assert.Equal(900, orden.GetProperty("duracionSegundos").GetInt32());
        Assert.Equal(7400, orden.GetProperty("distanciaMetros").GetInt32());

        // El pedido: los clientes de la base como trabajos, el promotor sale de donde está y no vuelve.
        var pedido = Assert.Single(proveedor.Pedidos);
        Assert.Equal("/optimization", pedido.Url.AbsolutePath);
        Assert.Equal(_clave, pedido.Clave);
        var trabajos = pedido.Cuerpo.GetProperty("jobs").EnumerateArray().ToArray();
        Assert.Equal(new[] { 1, 2, 3 }, trabajos.Select(t => t.GetProperty("id").GetInt32()));
        Assert.Equal(new[] { -63.178m, -17.772m }, trabajos[1].GetProperty("location").EnumerateArray().Select(c => c.GetDecimal()));
        var vehiculo = pedido.Cuerpo.GetProperty("vehicles")[0];
        Assert.Equal("foot-walking", vehiculo.GetProperty("profile").GetString());
        Assert.Equal(new[] { -63.19m, -17.78m }, vehiculo.GetProperty("start").EnumerateArray().Select(c => c.GetDecimal()));
        Assert.False(vehiculo.TryGetProperty("end", out _));
        Assert.True(pedido.Cuerpo.GetProperty("options").GetProperty("g").GetBoolean());

        // Nada cambió en la ruta.
        Assert.Equal(new[] { 1, 2, 3 }, Visitas(await (await r.ComoPromotor.GetAsync($"/api/rutas/{r.RutaId}")).LeerJsonAsync())
            .Select(v => v.Orden));
    }

    [Fact]
    public async Task Una_ausente_entra_al_orden_si_el_promotor_vuelve_a_pasar_y_las_hechas_van_primero()
    {
        var proveedor = new OpenRouteServiceFalso();
        await using var fabrica = api.ConNavegacion(proveedor, _clave);
        using var r = await RutaAsync();
        await MarcarAsync(r, r.Visitas[0], EstadoVisita.Visitado);
        await MarcarAsync(r, r.Visitas[1], EstadoVisita.Ausente);

        var orden = await (await OrdenAsync(fabrica, r, $"{DesdeElOrigen}&ausentes={r.Visitas[1]}")).LeerJsonAsync();

        // Ordena la ausente y la pendiente (al revés en el falso), después de la visitada.
        Assert.Equal(new[] { (r.Visitas[2], 2, false), (r.Visitas[1], 3, true) },
            orden.GetProperty("visitas").EnumerateArray().Select(v =>
                (v.GetProperty("id").GetInt32(), v.GetProperty("orden").GetInt32(), v.GetProperty("ausente").GetBoolean())));
        Assert.Equal(2, Assert.Single(proveedor.Pedidos).Cuerpo.GetProperty("jobs").GetArrayLength());
    }

    [Fact]
    public async Task Sin_dos_visitas_por_hacer_no_hay_nada_que_reordenar()
    {
        var proveedor = new OpenRouteServiceFalso();
        await using var fabrica = api.ConNavegacion(proveedor, _clave);
        using var r = await RutaAsync();
        await MarcarAsync(r, r.Visitas[0], EstadoVisita.Visitado);
        // La ausente no vuelve: queda una sola pendiente.
        await MarcarAsync(r, r.Visitas[1], EstadoVisita.Ausente);

        await (await OrdenAsync(fabrica, r)).ProblemaAsync(400, "nada_que_reordenar");
        Assert.Empty(proveedor.Pedidos);
    }

    [Fact]
    public async Task Una_ausente_que_ya_no_lo_es_responde_409_para_volver_a_calcular()
    {
        var proveedor = new OpenRouteServiceFalso();
        await using var fabrica = api.ConNavegacion(proveedor, _clave);
        using var r = await RutaAsync();

        await (await OrdenAsync(fabrica, r, $"{DesdeElOrigen}&ausentes={r.Visitas[0]}")).ProblemaAsync(409, "ruta_cambio");
    }

    [Fact]
    public async Task Una_visita_que_el_proveedor_no_puede_ordenar_va_al_final()
    {
        var proveedor = new OpenRouteServiceFalso
        {
            Responder = () => OpenRouteServiceFalso.Json(HttpStatusCode.OK, OpenRouteServiceFalso.OrdenCon([2], [1, 3]))
        };
        await using var fabrica = api.ConNavegacion(proveedor, _clave);
        using var r = await RutaAsync();

        var orden = await (await OrdenAsync(fabrica, r)).LeerJsonAsync();

        Assert.Equal(new[] { r.Visitas[1], r.Visitas[0], r.Visitas[2] },
            orden.GetProperty("visitas").EnumerateArray().Select(v => v.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task Solo_el_promotor_dueno_con_la_ruta_en_curso_pide_el_orden_y_reordena()
    {
        using var r = await RutaAsync();
        using var sinIniciar = await RutaAsync(enCurso: false);
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        var otro = await api.CrearUsuarioAsync(Roles.Promotor);
        using var comoSecretaria = api.ClienteCon(secretaria.Token);
        using var comoOtro = api.ClienteCon(otro.Token);

        await (await comoSecretaria.GetAsync($"/api/rutas/{r.RutaId}/orden-sugerido?{DesdeElOrigen}")).ProblemaAsync(403);
        await (await comoOtro.GetAsync($"/api/rutas/{r.RutaId}/orden-sugerido?{DesdeElOrigen}")).ProblemaAsync(404);
        await (await OrdenAsync(api, sinIniciar)).ProblemaAsync(400, "ruta_no_en_curso");
        await (await comoOtro.PostAsJsonAsync($"/api/rutas/{r.RutaId}/reordenar",
            new { visitas = r.Visitas, ausentesQueVuelven = Array.Empty<int>() })).ProblemaAsync(404);
        await (await ReordenarAsync(sinIniciar, sinIniciar.Visitas)).ProblemaAsync(400, "ruta_no_en_curso");
    }

    [Fact]
    public async Task Reordenar_pone_primero_las_hechas_por_la_hora_y_las_ausentes_que_vuelven_quedan_pendientes()
    {
        using var r = await RutaAsync(4);
        var (uno, dos, tres, cuatro) = (r.Visitas[0], r.Visitas[1], r.Visitas[2], r.Visitas[3]);
        await MarcarAsync(r, tres, EstadoVisita.Visitado);
        await MarcarAsync(r, uno, EstadoVisita.Ausente, "No estaba");
        await MarcarAsync(r, dos, EstadoVisita.Ausente);

        // Vuelve a pasar por la 1; la 2 sigue ausente y cuenta como hecha.
        var respuesta = await ReordenarAsync(r, [cuatro, uno], [uno]);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var ruta = await respuesta.LeerJsonAsync();
        Assert.Equal(new[] { (tres, 1, 1), (dos, 2, 2), (cuatro, 3, 0), (uno, 4, 0) }, Visitas(ruta));
        var vuelta = ruta.GetProperty("visitas").EnumerateArray().Single(v => v.GetProperty("id").GetInt32() == uno);
        Assert.Equal(JsonValueKind.Null, vuelta.GetProperty("horaVisita").ValueKind);
        Assert.Equal(JsonValueKind.Null, vuelta.GetProperty("observacion").ValueKind);
        Assert.Equal(JsonValueKind.Null, vuelta.GetProperty("latitud").ValueKind);
        Assert.Equal(2, ruta.GetProperty("pendientes").GetInt32());
    }

    [Fact]
    public async Task Reordenar_con_una_lista_que_no_es_la_de_lo_que_falta_responde_409()
    {
        using var r = await RutaAsync();
        var (uno, dos, tres) = (r.Visitas[0], r.Visitas[1], r.Visitas[2]);

        await (await ReordenarAsync(r, [uno, dos])).ProblemaAsync(409, "ruta_cambio");
        await (await ReordenarAsync(r, [uno, dos, tres, tres])).ProblemaAsync(409, "ruta_cambio");
        // Una "ausente que vuelve" que en realidad está pendiente.
        await (await ReordenarAsync(r, [tres, dos, uno], [uno])).ProblemaAsync(409, "ruta_cambio");
        // Se marcó una mientras tanto: ya no está entre las que faltan.
        await MarcarAsync(r, uno, EstadoVisita.Visitado);
        await (await ReordenarAsync(r, [tres, dos, uno])).ProblemaAsync(409, "ruta_cambio");

        Assert.Equal(new[] { (uno, 1, 1), (dos, 2, 0), (tres, 3, 0) },
            Visitas(await (await r.ComoPromotor.GetAsync($"/api/rutas/{r.RutaId}")).LeerJsonAsync()));
    }

    [Fact]
    public async Task Un_segundo_orden_antes_de_20_s_responde_503_sin_llamar_al_proveedor()
    {
        var proveedor = new OpenRouteServiceFalso();
        await using var fabrica = api.ConNavegacion(proveedor, _clave);
        using var r = await RutaAsync();

        Assert.Equal(HttpStatusCode.OK, (await OrdenAsync(fabrica, r)).StatusCode);
        var problema = await (await OrdenAsync(fabrica, r)).ProblemaAsync(503, NavegacionService.CodigoOrdenNoDisponible);

        Assert.Equal("No se pudo calcular el orden. Sigue con el que tienes.", problema.GetProperty("detail").GetString());
        Assert.Single(proveedor.Pedidos);
    }

    public static TheoryData<string> Fallas => new() { "500", "codigo 3", "sin clave" };

    [Theory]
    [MemberData(nameof(Fallas))]
    public async Task Si_el_proveedor_no_puede_el_orden_responde_503(string falla)
    {
        var proveedor = new OpenRouteServiceFalso
        {
            Responder = falla switch
            {
                "500" => () => OpenRouteServiceFalso.Json(HttpStatusCode.InternalServerError, new { error = "Fallo" }),
                "codigo 3" => () => OpenRouteServiceFalso.Json(HttpStatusCode.OK, new { code = 3, error = "Routing error" }),
                _ => null
            }
        };
        await using var fabrica = api.ConNavegacion(proveedor, falla == "sin clave" ? null : _clave);
        using var r = await RutaAsync();

        await (await OrdenAsync(fabrica, r)).ProblemaAsync(503, NavegacionService.CodigoOrdenNoDisponible);
        Assert.Equal(falla == "sin clave" ? 0 : 1, proveedor.Pedidos.Count);
    }

    [Fact]
    public void El_tope_diario_de_los_ordenes_es_aparte_del_de_los_caminos()
    {
        var cupo = new CupoDeNavegacion(new RelojAjustable(),
            Options.Create(new NavegacionOptions { TopeDiario = 1, TopeDiarioDeOrdenes = 1 }));

        Assert.True(cupo.Consumir(PedidoAlProveedor.Camino));
        Assert.False(cupo.Consumir(PedidoAlProveedor.Camino));
        Assert.True(cupo.Consumir(PedidoAlProveedor.Orden));
        Assert.False(cupo.Consumir(PedidoAlProveedor.Orden));
    }
}
