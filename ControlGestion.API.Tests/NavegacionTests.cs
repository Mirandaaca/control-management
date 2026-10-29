using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.DTOs;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ControlGestion.API.Tests;

/// <summary>
/// El camino por calles hasta una visita (tarea 25), con un OpenRouteService falso: ninguna prueba sale a internet. Cada
/// usuario pide un solo camino por prueba, salvo la del límite, porque el límite de uno cada 10 s es por usuario.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class NavegacionTests(ApiDePrueba api)
{
    private static readonly DateOnly Hoy = HoraBolivia.Hoy(TimeProvider.System);
    private const string DesdeElOrigen = "latitud=-17.78&longitud=-63.19";
    private readonly string _clave = $"clave-de-prueba-{Guid.NewGuid():N}";

    private sealed record Recorrido(UsuarioDePrueba Promotor, int RutaId, int[] Visitas, int[] Clientes);

    /// <summary>Un promotor con su zona y una ruta de hoy con un cliente nuevo; en curso, salvo que se pida lo contrario.</summary>
    private async Task<Recorrido> RutaAsync(bool enCurso = true)
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync(promotor.Id);
        int[] clientes = [await operador.CrearClienteAsync(zonaId)];
        var respuesta = await operador.PostAsJsonAsync("/api/rutas",
            new { zonaId, promotorId = promotor.Id, fecha = Hoy.ToString("yyyy-MM-dd"), clientes });
        respuesta.EnsureSuccessStatusCode();
        var ruta = await respuesta.LeerJsonAsync();
        var rutaId = ruta.GetProperty("id").GetInt32();
        if (enCurso)
        {
            using var comoPromotor = api.ClienteCon(promotor.Token);
            (await comoPromotor.PostAsync($"/api/rutas/{rutaId}/iniciar", null)).EnsureSuccessStatusCode();
        }
        var visitas = ruta.GetProperty("visitas").EnumerateArray().Select(v => v.GetProperty("id").GetInt32()).ToArray();
        return new Recorrido(promotor, rutaId, visitas, clientes);
    }

    private static string Camino(Recorrido r, string consulta = DesdeElOrigen) =>
        $"/api/rutas/{r.RutaId}/visitas/{r.Visitas[0]}/camino?{consulta}";

    private static async Task<HttpResponseMessage> PedirAsync(WebApplicationFactory<Program> fabrica, Recorrido r,
        string consulta = DesdeElOrigen)
    {
        using var cliente = fabrica.ClienteCon(r.Promotor.Token);
        return await cliente.GetAsync(Camino(r, consulta));
    }

    private static (decimal, decimal) Punto(JsonElement punto) =>
        (punto.GetProperty("latitud").GetDecimal(), punto.GetProperty("longitud").GetDecimal());

    private static decimal[] Coordenada(JsonElement cuerpo, int cual) =>
        cuerpo.GetProperty("coordinates")[cual].EnumerateArray().Select(c => c.GetDecimal()).ToArray();

    [Fact]
    public async Task El_camino_trae_la_linea_los_pasos_de_tu_y_las_alternativas()
    {
        var proveedor = new OpenRouteServiceFalso();
        await using var fabrica = api.ConNavegacion(proveedor, _clave);
        var r = await RutaAsync();

        var respuesta = await PedirAsync(fabrica, r);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var caminos = await respuesta.LeerJsonAsync();
        var principal = caminos.GetProperty("principal");
        Assert.Equal(1202, principal.GetProperty("distanciaMetros").GetInt32());
        Assert.Equal(146, principal.GetProperty("duracionSegundos").GetInt32());
        var puntos = principal.GetProperty("puntos").EnumerateArray().Select(Punto).ToArray();
        Assert.Equal(OpenRouteServiceFalso.Principal, puntos);

        var pasos = principal.GetProperty("pasos").EnumerateArray().ToArray();
        Assert.Equal(
            new[] { "Sigue por Calle Sucre", "Gira a la derecha en Calle 4",
                "En la rotonda, toma la 2.ª salida hacia Av. Banzer", "Llegas a tu destino" },
            pasos.Select(p => p.GetProperty("indicacion").GetString()));
        Assert.Equal(new[] { (int)Giro.Salida, (int)Giro.Derecha, (int)Giro.Rotonda, (int)Giro.Llegada },
            pasos.Select(p => p.GetProperty("giro").GetInt32()));
        var derecha = pasos[1];
        Assert.Equal("Calle 4", derecha.GetProperty("calle").GetString());
        Assert.Equal(365, derecha.GetProperty("distanciaMetros").GetInt32());
        Assert.Equal(45, derecha.GetProperty("duracionSegundos").GetInt32());
        Assert.Equal(1, derecha.GetProperty("indice").GetInt32());
        Assert.Equal(puntos[1], Punto(derecha.GetProperty("inicio")));
        Assert.Equal(JsonValueKind.Null, pasos[3].GetProperty("calle").ValueKind);

        var alternativa = Assert.Single(caminos.GetProperty("alternativas").EnumerateArray());
        Assert.Equal(190, alternativa.GetProperty("duracionSegundos").GetInt32());
        Assert.Equal(OpenRouteServiceFalso.Alternativa, alternativa.GetProperty("puntos").EnumerateArray().Select(Punto));
        var izquierda = alternativa.GetProperty("pasos")[1];
        Assert.Equal("Gira a la izquierda", izquierda.GetProperty("indicacion").GetString());
        Assert.Equal(JsonValueKind.Null, izquierda.GetProperty("calle").ValueKind);
    }

    // OpenRouteService no tiene perfil de moto: la moto va como auto.
    [Theory]
    [InlineData("auto", "driving-car")]
    [InlineData("moto", "driving-car")]
    [InlineData("pie", "foot-walking")]
    public async Task El_pedido_lleva_el_perfil_del_medio_el_idioma_las_alternativas_y_el_cliente_de_la_base(string medio,
        string perfil)
    {
        var proveedor = new OpenRouteServiceFalso();
        await using var fabrica = api.ConNavegacion(proveedor, _clave);
        var r = await RutaAsync();
        using (var scope = api.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Clientes.Where(c => c.Id == r.Clientes[0])
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Latitud, -17.7701m).SetProperty(c => c.Longitud, -63.1702m));

        Assert.Equal(HttpStatusCode.OK, (await PedirAsync(fabrica, r, $"{DesdeElOrigen}&medio={medio}")).StatusCode);

        var pedido = Assert.Single(proveedor.Pedidos);
        Assert.Equal($"/v2/directions/{perfil}/json", pedido.Url.AbsolutePath);
        Assert.Equal(_clave, pedido.Clave);
        Assert.DoesNotContain(_clave, pedido.Url.ToString());
        // [longitud, latitud]: el origen del celular y el destino del cliente en la base.
        Assert.Equal(new[] { -63.19m, -17.78m }, Coordenada(pedido.Cuerpo, 0));
        Assert.Equal(new[] { -63.1702m, -17.7701m }, Coordenada(pedido.Cuerpo, 1));
        Assert.Equal("es", pedido.Cuerpo.GetProperty("language").GetString());
        Assert.Equal("text", pedido.Cuerpo.GetProperty("instructions_format").GetString());
        Assert.Equal(3, pedido.Cuerpo.GetProperty("alternative_routes").GetProperty("target_count").GetInt32());
    }

    [Fact]
    public async Task Sin_el_permiso_de_recorrer_rutas_no_hay_camino()
    {
        var r = await RutaAsync();
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        using var cliente = api.ClienteCon(secretaria.Token);

        await (await cliente.GetAsync(Camino(r))).ProblemaAsync(403);
    }

    [Fact]
    public async Task Otro_promotor_no_ve_el_camino_de_una_ruta_ajena()
    {
        var r = await RutaAsync();
        var otro = await api.CrearUsuarioAsync(Roles.Promotor);
        using var cliente = api.ClienteCon(otro.Token);

        await (await cliente.GetAsync(Camino(r))).ProblemaAsync(404);
    }

    [Fact]
    public async Task Una_ruta_sin_iniciar_no_tiene_camino()
    {
        var r = await RutaAsync(enCurso: false);

        await (await PedirAsync(api, r)).ProblemaAsync(400, "ruta_no_en_curso");
    }

    [Fact]
    public async Task Una_visita_de_otra_ruta_no_tiene_camino()
    {
        var r = await RutaAsync();
        var otra = await RutaAsync();

        await (await PedirAsync(api, r with { Visitas = otra.Visitas })).ProblemaAsync(404);
    }

    [Theory]
    [InlineData("longitud=-63.19", "latitud")]
    [InlineData("latitud=95&longitud=-63.19", "latitud")]
    [InlineData("latitud=0&longitud=0", "latitud")]
    [InlineData(DesdeElOrigen + "&medio=avion", "medio")]
    [InlineData(DesdeElOrigen + "&medio=7", "medio")]
    public async Task Sin_una_ubicacion_o_un_medio_validos_no_se_pide_el_camino(string consulta, string campo)
    {
        var r = await RutaAsync();

        var problema = await (await PedirAsync(api, r, consulta)).ProblemaAsync(400, "validacion");

        Assert.True(problema.GetProperty("errors").TryGetProperty(campo, out _), $"Falta el error de '{campo}'.");
    }

    [Fact]
    public async Task Un_segundo_camino_antes_de_10_s_responde_503_sin_llamar_al_proveedor()
    {
        var proveedor = new OpenRouteServiceFalso();
        await using var fabrica = api.ConNavegacion(proveedor, _clave);
        var r = await RutaAsync();

        Assert.Equal(HttpStatusCode.OK, (await PedirAsync(fabrica, r)).StatusCode);
        var problema = await (await PedirAsync(fabrica, r, "latitud=-17.75&longitud=-63.15"))
            .ProblemaAsync(503, NavegacionService.CodigoNoDisponible);

        Assert.Equal("No se pudo calcular el camino. Sigue el mapa.", problema.GetProperty("detail").GetString());
        Assert.Single(proveedor.Pedidos);
    }

    [Fact]
    public async Task Dos_pedidos_desde_casi_el_mismo_punto_comparten_el_camino_del_cache()
    {
        var proveedor = new OpenRouteServiceFalso();
        await using var fabrica = api.ConNavegacion(proveedor, _clave);
        // Los dos clientes están en el mismo punto (CrearClienteAsync), y los dos orígenes redondean a -17.78, -63.19.
        var uno = await RutaAsync();
        var otro = await RutaAsync();

        Assert.Equal(HttpStatusCode.OK, (await PedirAsync(fabrica, uno, "latitud=-17.78001&longitud=-63.19002")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PedirAsync(fabrica, otro, "latitud=-17.77996&longitud=-63.18997")).StatusCode);

        var pedido = Assert.Single(proveedor.Pedidos);
        Assert.Equal(new[] { -63.19m, -17.78m }, Coordenada(pedido.Cuerpo, 0));
    }

    [Fact]
    public async Task Pasado_el_tope_diario_el_camino_responde_503()
    {
        var proveedor = new OpenRouteServiceFalso();
        await using var fabrica = api.ConNavegacion(proveedor, _clave,
            ajustes: new Dictionary<string, string> { ["Navegacion:TopeDiario"] = "1" });
        var uno = await RutaAsync();
        var otro = await RutaAsync();

        Assert.Equal(HttpStatusCode.OK, (await PedirAsync(fabrica, uno)).StatusCode);
        await (await PedirAsync(fabrica, otro, "latitud=-17.75&longitud=-63.15"))
            .ProblemaAsync(503, NavegacionService.CodigoNoDisponible);

        Assert.Single(proveedor.Pedidos);
    }

    [Fact]
    public void El_tope_diario_vuelve_a_empezar_el_dia_siguiente_de_Bolivia()
    {
        var reloj = new RelojAjustable();
        var cupo = new CupoDeNavegacion(reloj, Options.Create(new NavegacionOptions { TopeDiario = 2 }));

        Assert.True(cupo.Consumir());
        Assert.True(cupo.Consumir());
        Assert.False(cupo.Consumir());

        reloj.Adelantar(TimeSpan.FromDays(1));
        Assert.True(cupo.Consumir());
    }

    public static TheoryData<string> Fallas => new() { "500", "429", "403", "404", "sin caminos", "red", "tiempo" };

    private static Func<HttpResponseMessage> Falla(string cual) => cual switch
    {
        "500" => () => OpenRouteServiceFalso.Json(HttpStatusCode.InternalServerError,
            new { error = new { code = 2099, message = "Unknown internal error." } }),
        "429" => () => OpenRouteServiceFalso.Json(HttpStatusCode.TooManyRequests, new { error = "Rate limit exceeded" }),
        "403" => () => OpenRouteServiceFalso.Json(HttpStatusCode.Forbidden,
            new { error = "Access to this API has been disallowed" }),
        "404" => () => OpenRouteServiceFalso.Json(HttpStatusCode.NotFound,
            new { error = new { code = 2010, message = "Could not find routable point within a radius of 350.0 meters." } }),
        "sin caminos" => () => OpenRouteServiceFalso.Json(HttpStatusCode.OK, new { routes = Array.Empty<object>() }),
        "red" => () => throw new HttpRequestException("No such host is known."),
        "tiempo" => () => throw new TaskCanceledException("Se agotó el tiempo.", new TimeoutException()),
        _ => throw new ArgumentOutOfRangeException(nameof(cual))
    };

    [Theory]
    [MemberData(nameof(Fallas))]
    public async Task Si_el_proveedor_falla_el_camino_responde_503_para_seguir_el_mapa(string falla)
    {
        var proveedor = new OpenRouteServiceFalso { Responder = Falla(falla) };
        await using var fabrica = api.ConNavegacion(proveedor, _clave);
        var r = await RutaAsync();

        var problema = await (await PedirAsync(fabrica, r)).ProblemaAsync(503, NavegacionService.CodigoNoDisponible);

        Assert.Equal("No se pudo calcular el camino. Sigue el mapa.", problema.GetProperty("detail").GetString());
        Assert.Single(proveedor.Pedidos);
    }

    [Fact]
    public async Task Sin_la_clave_el_camino_responde_503_sin_llamar_al_proveedor()
    {
        var proveedor = new OpenRouteServiceFalso();
        await using var fabrica = api.ConNavegacion(proveedor, clave: null);
        var r = await RutaAsync();

        await (await PedirAsync(fabrica, r)).ProblemaAsync(503, NavegacionService.CodigoNoDisponible);

        Assert.Empty(proveedor.Pedidos);
    }

    [Fact]
    public async Task La_clave_no_aparece_en_ningun_log()
    {
        var proveedor = new OpenRouteServiceFalso();
        var logs = new LogsCapturados();
        await using var fabrica = api.ConNavegacion(proveedor, _clave, logs);
        var bien = await RutaAsync();
        var mal = await RutaAsync();

        Assert.Equal(HttpStatusCode.OK, (await PedirAsync(fabrica, bien)).StatusCode);
        proveedor.Responder = Falla("500");
        await (await PedirAsync(fabrica, mal, "latitud=-17.75&longitud=-63.15")).ProblemaAsync(503);

        // El log de HttpClient, con los encabezados del pedido, se capturó (desde Trace) y no trae la clave.
        Assert.Contains(logs.Lineas, l => l.Contains("System.Net.Http.HttpClient.NavegacionService"));
        Assert.Contains(logs.Lineas, l => l.Contains("OpenRouteService respondió 500 (código 2099)."));
        Assert.DoesNotContain(logs.Lineas, l => l.Contains(_clave));
        Assert.DoesNotContain(logs.Lineas, l => l.Contains("Error no controlado"));
    }
}
