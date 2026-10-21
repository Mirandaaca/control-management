using System.Net;
using System.Net.Http.Json;
using ControlGestion.API.Data;
using ControlGestion.API.Services;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class NumeracionTests(ApiDePrueba api)
{
    [Fact]
    public async Task Diez_HP_creadas_a_la_vez_reciben_numeros_distintos()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var productoId = await operador.CrearProductoAsync(5m);
        var clienteId = await operador.CrearClienteAsync(zonaId);

        var respuestas = await Ayudantes.EnviarALaVezAsync(10, _ => operador.PostAsJsonAsync("/api/hojas-pedido", new
        {
            clienteId, latitud = -17.78m, longitud = -63.18m, tipoVenta = 0,
            detalles = new[] { new { productoId, cantidad = 1 } }
        }));

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var numeros = await Task.WhenAll(respuestas.Select(async r => (await r.LeerJsonAsync()).GetProperty("numero").GetString()));
        Assert.Equal(10, numeros.Distinct().Count());
    }

    [Fact]
    public async Task Ocho_clientes_creados_a_la_vez_reciben_codigos_distintos()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();

        var respuestas = await Ayudantes.EnviarALaVezAsync(8, i => operador.PostAsJsonAsync("/api/clientes", new
        {
            nombreCompleto = $"Cliente simultáneo {i}", telefono = $"7100000{i}", direccionTexto = "Calle",
            latitud = -17.78m, longitud = -63.18m, zonaId
        }));

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var codigos = await Task.WhenAll(respuestas.Select(async r => (await r.LeerJsonAsync()).GetProperty("codigo").GetString()));
        Assert.Equal(8, codigos.Distinct().Count());
    }

    [Fact]
    public async Task La_numeracion_reinicia_con_el_anio_de_Bolivia()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var prefijo = Ayudantes.Unico("T")[..6];

        // 31/12/2030 a las 23:30 en Bolivia (03:30 UTC del 1 de enero): todavía es 2030.
        var finDe2030 = new NumeradorService(db, new RelojFijo(new DateTimeOffset(2031, 1, 1, 3, 30, 0, TimeSpan.Zero)));
        // 1/1/2031 a las 00:30 en Bolivia: ya es 2031 y la secuencia empieza de nuevo.
        var inicioDe2031 = new NumeradorService(db, new RelojFijo(new DateTimeOffset(2031, 1, 1, 4, 30, 0, TimeSpan.Zero)));

        Assert.Equal($"{prefijo}-2030-00001", await finDe2030.SiguienteDelAnioAsync(prefijo));
        Assert.Equal($"{prefijo}-2030-00002", await finDe2030.SiguienteDelAnioAsync(prefijo));
        Assert.Equal($"{prefijo}-2031-00001", await inicioDe2031.SiguienteDelAnioAsync(prefijo));
    }
}
