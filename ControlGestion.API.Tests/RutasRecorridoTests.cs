using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>El recorrido de la ruta (7.3.20, pasos 3 a 7), que la app usa en la tarea 17, y su seguimiento (paso 6).</summary>
[Collection(ColeccionApi.Nombre)]
public class RutasRecorridoTests(ApiDePrueba api)
{
    private static readonly DateOnly Hoy = HoraBolivia.Hoy(TimeProvider.System);

    [Fact]
    public async Task El_proceso_diario_borra_las_ubicaciones_de_hace_mas_de_30_dias()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var ahora = DateTime.UtcNow;
        UbicacionPromotor En(DateTime fecha) => new() { PromotorId = promotor.Id, Latitud = -17.78m, Longitud = -63.18m, Fecha = fecha };
        var hace31 = En(ahora.AddDays(-31));
        var hace29 = En(ahora.AddDays(-29));
        var hoy = En(ahora.AddMinutes(-1));

        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UbicacionesPromotor.AddRange(hace31, hace29, hoy);
        await db.SaveChangesAsync();

        await ActivatorUtilities.CreateInstance<RutaService>(scope.ServiceProvider, new RelojFijo(new DateTimeOffset(ahora)))
            .BorrarUbicacionesViejasAsync();

        var quedan = await db.UbicacionesPromotor.AsNoTracking().Where(u => u.PromotorId == promotor.Id).Select(u => u.Id).ToListAsync();
        Assert.Equal(new[] { hace29.Id, hoy.Id }.Order(), quedan.Order());
    }

    private sealed record Escenario(UsuarioDePrueba Promotor, HttpClient ComoPromotor, int RutaId, int[] Visitas, int[] Clientes)
        : IDisposable
    {
        public void Dispose() => ComoPromotor.Dispose();
    }

    /// <summary>Un promotor con su zona y una ruta planificada para ese día con tantos clientes nuevos (nunca visitados).</summary>
    private async Task<Escenario> RutaAsync(int clientes = 3, DateOnly? dia = null)
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync(promotor.Id);
        var ids = new int[clientes];
        for (var i = 0; i < clientes; i++) ids[i] = await operador.CrearClienteAsync(zonaId);
        var respuesta = await operador.PostAsJsonAsync("/api/rutas",
            new { zonaId, promotorId = promotor.Id, fecha = (dia ?? Hoy).ToString("yyyy-MM-dd"), clientes = ids });
        respuesta.EnsureSuccessStatusCode();
        var ruta = await respuesta.LeerJsonAsync();
        var visitas = ruta.GetProperty("visitas").EnumerateArray().Select(v => v.GetProperty("id").GetInt32()).ToArray();
        return new Escenario(promotor, api.ClienteCon(promotor.Token), ruta.GetProperty("id").GetInt32(), visitas, ids);
    }

    private static Task<HttpResponseMessage> MarcarAsync(HttpClient cliente, int rutaId, int visitaId, EstadoVisita resultado,
        string? observacion = null) =>
        cliente.PostAsJsonAsync($"/api/rutas/{rutaId}/visitas/{visitaId}",
            new { resultado = (int)resultado, latitud = -17.7834m, longitud = -63.1822m, observacion });

    private static async Task<JsonElement> RutaAsync(HttpClient cliente, int rutaId) =>
        await (await cliente.GetAsync($"/api/rutas/{rutaId}")).LeerJsonAsync();

    private static DateTime? Fecha(JsonElement json, string propiedad) =>
        json.GetProperty(propiedad).ValueKind == JsonValueKind.Null
            ? null
            : DateTime.SpecifyKind(json.GetProperty(propiedad).GetDateTime(), DateTimeKind.Utc);

    [Fact]
    public async Task El_promotor_inicia_marca_y_termina_y_lo_que_queda_sin_marcar_queda_pendiente()
    {
        using var e = await RutaAsync();

        var iniciada = await (await e.ComoPromotor.PostAsync($"/api/rutas/{e.RutaId}/iniciar", null)).LeerJsonAsync();
        Assert.Equal((int)EstadoRuta.EnCurso, iniciada.GetProperty("estado").GetInt32());
        Assert.NotNull(Fecha(iniciada, "fechaInicio"));
        var otraVez = await (await e.ComoPromotor.PostAsync($"/api/rutas/{e.RutaId}/iniciar", null)).ProblemaAsync(400, "ruta_ya_iniciada");
        Assert.Equal("La ruta ya está en curso.", otraVez.GetProperty("detail").GetString());

        (await MarcarAsync(e.ComoPromotor, e.RutaId, e.Visitas[0], EstadoVisita.Visitado, "Compró arroz.")).EnsureSuccessStatusCode();
        var marcada = await (await MarcarAsync(e.ComoPromotor, e.RutaId, e.Visitas[1], EstadoVisita.Ausente)).LeerJsonAsync();
        Assert.Equal(2, marcada.GetProperty("marcadas").GetInt32());
        Assert.Equal(1, marcada.GetProperty("visitadas").GetInt32());
        var primera = marcada.GetProperty("visitas")[0];
        Assert.Equal((int)EstadoVisita.Visitado, primera.GetProperty("resultado").GetInt32());
        Assert.Equal("Compró arroz.", primera.GetProperty("observacion").GetString());
        Assert.Equal(-17.7834m, primera.GetProperty("latitud").GetDecimal());
        Assert.NotNull(Fecha(primera, "horaVisita"));

        var terminada = await (await e.ComoPromotor.PostAsync($"/api/rutas/{e.RutaId}/terminar", null)).LeerJsonAsync();
        Assert.Equal((int)EstadoRuta.Completada, terminada.GetProperty("estado").GetInt32());
        Assert.NotNull(Fecha(terminada, "fechaFin"));
        Assert.Equal(1, terminada.GetProperty("pendientes").GetInt32());
        Assert.Equal((int)EstadoVisita.Pendiente, terminada.GetProperty("visitas")[2].GetProperty("resultado").GetInt32());
        var completada = await (await e.ComoPromotor.PostAsync($"/api/rutas/{e.RutaId}/iniciar", null)).ProblemaAsync(400, "ruta_ya_iniciada");
        Assert.Equal("La ruta ya se completó.", completada.GetProperty("detail").GetString());
        await (await MarcarAsync(e.ComoPromotor, e.RutaId, e.Visitas[2], EstadoVisita.Visitado)).ProblemaAsync(400, "ruta_no_en_curso");
    }

    // 7.3.20, paso 5.3 y ext. 5.3a: visitado es una visita del cliente; ausente, no.
    [Fact]
    public async Task Visitado_pasa_al_cliente_a_activo_con_su_proxima_visita_y_ausente_no_lo_cambia()
    {
        using var e = await RutaAsync(2);
        using var operador = await api.OperadorAsync();
        (await e.ComoPromotor.PostAsync($"/api/rutas/{e.RutaId}/iniciar", null)).EnsureSuccessStatusCode();

        await MarcarAsync(e.ComoPromotor, e.RutaId, e.Visitas[0], EstadoVisita.Visitado);
        await MarcarAsync(e.ComoPromotor, e.RutaId, e.Visitas[1], EstadoVisita.Ausente);

        var visitado = await (await operador.GetAsync($"/api/clientes/{e.Clientes[0]}")).LeerJsonAsync();
        Assert.Equal("Activo", visitado.GetProperty("estado").GetProperty("nombre").GetString());
        Assert.Equal(TimeSpan.FromDays(15), Fecha(visitado, "proximaVisita") - Fecha(visitado, "ultimaVisita"));
        var ausente = await (await operador.GetAsync($"/api/clientes/{e.Clientes[1]}")).LeerJsonAsync();
        Assert.Equal("Disponible para re-visita", ausente.GetProperty("estado").GetProperty("nombre").GetString());
        Assert.Null(Fecha(ausente, "ultimaVisita"));
    }

    [Fact]
    public async Task Un_estado_asignado_a_mano_no_cambia_con_la_visita()
    {
        using var e = await RutaAsync(1);
        using (var scope = api.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Clientes.Where(c => c.Id == e.Clientes[0])
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.EstadoClienteId, EstadosDeCliente.Inactivo));
        (await e.ComoPromotor.PostAsync($"/api/rutas/{e.RutaId}/iniciar", null)).EnsureSuccessStatusCode();

        (await MarcarAsync(e.ComoPromotor, e.RutaId, e.Visitas[0], EstadoVisita.Visitado)).EnsureSuccessStatusCode();

        using var operador = await api.OperadorAsync();
        var cliente = await (await operador.GetAsync($"/api/clientes/{e.Clientes[0]}")).LeerJsonAsync();
        Assert.Equal("Inactivo", cliente.GetProperty("estado").GetProperty("nombre").GetString());
        Assert.NotNull(Fecha(cliente, "ultimaVisita"));
    }

    // Decisión de la tarea 14: el promotor vuelve en la tarde y el ausente pasa a visitado; un visitado no cambia.
    [Fact]
    public async Task Un_ausente_se_vuelve_a_marcar_y_un_visitado_no()
    {
        using var e = await RutaAsync(1);
        (await e.ComoPromotor.PostAsync($"/api/rutas/{e.RutaId}/iniciar", null)).EnsureSuccessStatusCode();
        await MarcarAsync(e.ComoPromotor, e.RutaId, e.Visitas[0], EstadoVisita.Ausente);

        var deNuevo = await (await MarcarAsync(e.ComoPromotor, e.RutaId, e.Visitas[0], EstadoVisita.Visitado)).LeerJsonAsync();
        await (await MarcarAsync(e.ComoPromotor, e.RutaId, e.Visitas[0], EstadoVisita.Ausente)).ProblemaAsync(400, "visita_ya_marcada");

        Assert.Equal((int)EstadoVisita.Visitado, deNuevo.GetProperty("visitas")[0].GetProperty("resultado").GetInt32());
    }

    [Fact]
    public async Task Sin_ubicacion_no_se_marca_la_visita_ni_antes_de_iniciar()
    {
        using var e = await RutaAsync(1);
        await (await MarcarAsync(e.ComoPromotor, e.RutaId, e.Visitas[0], EstadoVisita.Visitado)).ProblemaAsync(400, "ruta_no_en_curso");
        (await e.ComoPromotor.PostAsync($"/api/rutas/{e.RutaId}/iniciar", null)).EnsureSuccessStatusCode();

        var problema = await (await e.ComoPromotor.PostAsJsonAsync($"/api/rutas/{e.RutaId}/visitas/{e.Visitas[0]}",
            new { resultado = 1, observacion = "Sin GPS" })).ProblemaAsync(400, "validacion");

        Assert.Equal("Sin ubicación no se marca la visita: activa el GPS.",
            problema.GetProperty("errors").GetProperty("latitud")[0].GetString());
        Assert.Equal(0, (await RutaAsync(e.ComoPromotor, e.RutaId)).GetProperty("marcadas").GetInt32());
    }

    [Fact]
    public async Task Solo_el_promotor_de_la_ruta_la_recorre()
    {
        using var e = await RutaAsync(1);
        using var operador = await api.OperadorAsync();
        var otro = await api.CrearUsuarioAsync(Roles.Promotor);
        using var comoOtro = api.ClienteCon(otro.Token);

        // El operador ve todas las rutas (cartera completa), pero no es el dueño; otro promotor ni siquiera la ve.
        await (await operador.PostAsync($"/api/rutas/{e.RutaId}/iniciar", null)).ProblemaAsync(400, "ruta_de_otro_promotor");
        await (await comoOtro.PostAsync($"/api/rutas/{e.RutaId}/iniciar", null)).ProblemaAsync(404);
        await (await comoOtro.PostAsJsonAsync($"/api/rutas/{e.RutaId}/ubicaciones", new { latitud = -17.78m, longitud = -63.18m }))
            .ProblemaAsync(404);
    }

    // Pasos 4.3, 6.1 y 6.2, y ext. 6.1a: el panel muestra la última ubicación desde que empezó la ruta.
    [Fact]
    public async Task El_seguimiento_muestra_la_ultima_ubicacion_desde_el_inicio_y_al_terminar_ya_no_se_reciben()
    {
        using var e = await RutaAsync(1);
        Task<HttpResponseMessage> UbicacionAsync(decimal latitud) =>
            e.ComoPromotor.PostAsJsonAsync($"/api/rutas/{e.RutaId}/ubicaciones", new { latitud, longitud = -63.18m });
        await (await UbicacionAsync(-17.70m)).ProblemaAsync(400, "ruta_no_en_curso");
        using (var scope = api.Services.CreateScope())
        {
            // Una de ayer (otra ruta) no cuenta.
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UbicacionesPromotor.Add(new UbicacionPromotor
                { PromotorId = e.Promotor.Id, Latitud = -17.60m, Longitud = -63.10m, Fecha = DateTime.UtcNow.AddDays(-1) });
            await db.SaveChangesAsync();
        }
        (await e.ComoPromotor.PostAsync($"/api/rutas/{e.RutaId}/iniciar", null)).EnsureSuccessStatusCode();
        using var operador = await api.OperadorAsync();

        Assert.Equal(JsonValueKind.Null, (await RutaAsync(operador, e.RutaId)).GetProperty("ultimaUbicacion").ValueKind);

        Assert.Equal(204, (int)(await UbicacionAsync(-17.71m)).StatusCode);
        Assert.Equal(204, (int)(await UbicacionAsync(-17.72m)).StatusCode);
        var ultima = (await RutaAsync(operador, e.RutaId)).GetProperty("ultimaUbicacion");
        Assert.Equal(-17.72m, ultima.GetProperty("latitud").GetDecimal());
        Assert.NotNull(Fecha(ultima, "fecha"));

        (await e.ComoPromotor.PostAsync($"/api/rutas/{e.RutaId}/terminar", null)).EnsureSuccessStatusCode();
        await (await UbicacionAsync(-17.73m)).ProblemaAsync(400, "ruta_no_en_curso");
        Assert.Equal(-17.72m, (await RutaAsync(operador, e.RutaId)).GetProperty("ultimaUbicacion").GetProperty("latitud").GetDecimal());
    }

    [Fact]
    public async Task La_ruta_del_dia_es_la_de_hoy_del_promotor()
    {
        using var manana = await RutaAsync(1, Hoy.AddDays(1));
        await (await manana.ComoPromotor.GetAsync("/api/rutas/del-dia")).ProblemaAsync(404, "sin_ruta_del_dia");

        using var hoy = await RutaAsync(2);
        var ruta = await (await hoy.ComoPromotor.GetAsync("/api/rutas/del-dia")).LeerJsonAsync();

        Assert.Equal(hoy.RutaId, ruta.GetProperty("id").GetInt32());
        // Un cliente nunca visitado no tiene atraso: la app lo muestra como "Sin visitas".
        Assert.Equal(JsonValueKind.Null, ruta.GetProperty("visitas")[0].GetProperty("diasDeAtraso").ValueKind);
        Assert.Equal("Calle de prueba", ruta.GetProperty("visitas")[0].GetProperty("cliente").GetProperty("direccionTexto").GetString());
    }
}
