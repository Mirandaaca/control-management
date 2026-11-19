using System.Net;
using System.Net.Http.Json;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>
/// La ruta de otra zona (arreglo del 18/11/2026): el gerente puede planificar la ruta de una zona para un promotor que no
/// es el suyo (7.3.20, paso 2.1). Ese día, los clientes de la ruta entran en la cartera de quien la recorre: registra el
/// evento de la visita ausente (ext. 5.3a) y la venta de la visitada (ext. 5a). Antes marcaba la visita y el evento
/// respondía "No se encontró el cliente.".
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class CarteraDeLaRutaDelDiaTests(ApiDePrueba api)
{
    private static DateOnly Hoy => HoraBolivia.Hoy(TimeProvider.System);

    private sealed record Escenario(HttpClient QuienRecorre, int RutaId, int VisitaId, int EnLaRuta, int FueraDeLaRuta)
        : IDisposable
    {
        public void Dispose() => QuienRecorre.Dispose();
    }

    /// <summary>
    /// Una zona con su promotor y dos clientes, y la ruta de ese día para otro promotor con uno de ellos. Sin día, hoy.
    /// </summary>
    private async Task<Escenario> RutaDeOtraZonaAsync(DateOnly? dia = null)
    {
        var duenoDeLaZona = await api.CrearUsuarioAsync(Roles.Promotor);
        var quienRecorre = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync(duenoDeLaZona.Id);
        var enLaRuta = await operador.CrearClienteAsync(zonaId);
        var fueraDeLaRuta = await operador.CrearClienteAsync(zonaId);
        var respuesta = await operador.PostAsJsonAsync("/api/rutas", new
        {
            zonaId, promotorId = quienRecorre.Id, fecha = (dia ?? Hoy).ToString("yyyy-MM-dd"), clientes = new[] { enLaRuta }
        });
        respuesta.EnsureSuccessStatusCode();
        var ruta = await respuesta.LeerJsonAsync();
        return new Escenario(api.ClienteCon(quienRecorre.Token), ruta.GetProperty("id").GetInt32(),
            ruta.GetProperty("visitas")[0].GetProperty("id").GetInt32(), enLaRuta, fueraDeLaRuta);
    }

    private static Task<HttpResponseMessage> EventoAsync(HttpClient promotor, int clienteId) =>
        promotor.PostAsJsonAsync("/api/eventos", new
        {
            tipoEventoId = 3, clienteId, latitud = -17.7834m, longitud = -63.1822m, descripcion = "Arreglo: el cliente estaba ausente"
        });

    [Fact]
    public async Task Quien_recorre_la_ruta_de_otra_zona_registra_el_evento_y_la_venta_de_sus_visitas()
    {
        using var e = await RutaDeOtraZonaAsync();
        (await e.QuienRecorre.PostAsync($"/api/rutas/{e.RutaId}/iniciar", null)).EnsureSuccessStatusCode();
        var marca = await e.QuienRecorre.PostAsJsonAsync($"/api/rutas/{e.RutaId}/visitas/{e.VisitaId}",
            new { resultado = 2, latitud = -17.7834m, longitud = -63.1822m });
        Assert.Equal(HttpStatusCode.OK, marca.StatusCode);

        // La visita ausente: el evento (ext. 5.3a).
        Assert.Equal(HttpStatusCode.Created, (await EventoAsync(e.QuienRecorre, e.EnLaRuta)).StatusCode);
        // La visitada: el cliente para la venta y la HP (ext. 5a).
        Assert.Equal(HttpStatusCode.OK, (await e.QuienRecorre.GetAsync($"/api/clientes/{e.EnLaRuta}")).StatusCode);
        using var operador = await api.OperadorAsync();
        await e.QuienRecorre.CrearHojaPedidoAsync(e.EnLaRuta, await operador.CrearProductoAsync(10m), 1);
    }

    [Fact]
    public async Task Solo_los_clientes_de_la_ruta_no_toda_la_zona_y_tambien_en_la_lista()
    {
        using var e = await RutaDeOtraZonaAsync();

        await (await e.QuienRecorre.GetAsync($"/api/clientes/{e.FueraDeLaRuta}")).ProblemaAsync(404);
        await (await EventoAsync(e.QuienRecorre, e.FueraDeLaRuta)).ProblemaAsync(400, "cliente_no_existe");
        var lista = (await (await e.QuienRecorre.GetAsync("/api/clientes?tamano=100")).LeerJsonAsync())
            .GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).ToList();
        Assert.Contains(e.EnLaRuta, lista);
        Assert.DoesNotContain(e.FueraDeLaRuta, lista);
    }

    [Fact]
    public async Task Terminada_la_ruta_ese_dia_todavia_registra_el_evento()
    {
        using var e = await RutaDeOtraZonaAsync();
        (await e.QuienRecorre.PostAsync($"/api/rutas/{e.RutaId}/iniciar", null)).EnsureSuccessStatusCode();
        (await e.QuienRecorre.PostAsync($"/api/rutas/{e.RutaId}/terminar", null)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Created, (await EventoAsync(e.QuienRecorre, e.EnLaRuta)).StatusCode);
    }

    [Fact]
    public async Task Una_ruta_de_otro_dia_no_suma_sus_clientes()
    {
        // La de mañana todavía no cuenta.
        using var deManana = await RutaDeOtraZonaAsync(Hoy.AddDays(1));
        await (await deManana.QuienRecorre.GetAsync($"/api/clientes/{deManana.EnLaRuta}")).ProblemaAsync(404);
        await (await EventoAsync(deManana.QuienRecorre, deManana.EnLaRuta)).ProblemaAsync(400, "cliente_no_existe");

        // La de ayer ya no cuenta (la API no planifica en el pasado: se mueve en la base).
        using var deAyer = await RutaDeOtraZonaAsync();
        using (var scope = api.Services.CreateScope())
        {
            var ayer = Hoy.AddDays(-1);
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Rutas.Where(r => r.Id == deAyer.RutaId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.FechaPlanificada, ayer));
        }
        await (await deAyer.QuienRecorre.GetAsync($"/api/clientes/{deAyer.EnLaRuta}")).ProblemaAsync(404);
        await (await EventoAsync(deAyer.QuienRecorre, deAyer.EnLaRuta)).ProblemaAsync(400, "cliente_no_existe");
    }
}
