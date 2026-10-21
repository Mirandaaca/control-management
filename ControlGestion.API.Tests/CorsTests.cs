using ControlGestion.API.Tests.Infraestructura;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class CorsTests(ApiDePrueba api)
{
    private async Task<HttpResponseMessage> PreflightAsync(string origen)
    {
        using var cliente = api.CreateClient();
        var pedido = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        pedido.Headers.Add("Origin", origen);
        pedido.Headers.Add("Access-Control-Request-Method", "POST");
        pedido.Headers.Add("Access-Control-Request-Headers", "content-type");
        return await cliente.SendAsync(pedido);
    }

    [Fact]
    public async Task Solo_los_origenes_de_la_configuracion_pueden_llamar_desde_un_navegador()
    {
        var permitido = await PreflightAsync(ApiDePrueba.OrigenDePrueba);
        Assert.Equal(ApiDePrueba.OrigenDePrueba, Assert.Single(permitido.Headers.GetValues("Access-Control-Allow-Origin")));

        var otro = await PreflightAsync("http://otro-origen.local");
        Assert.False(otro.Headers.Contains("Access-Control-Allow-Origin"));
    }

    // Si appsettings.json llevara su propia lista, .NET la mezclaría con la de Development posición por posición.
    [Fact]
    public void En_desarrollo_se_permiten_el_panel_y_la_app_web()
    {
        var carpetaDeLaApi = api.Services.GetRequiredService<IHostEnvironment>().ContentRootPath;
        var configuracion = new ConfigurationBuilder()
            .SetBasePath(carpetaDeLaApi)
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json")
            .Build();

        Assert.Equal(
            ["http://localhost:5173", "http://localhost:5174", "http://localhost:5175"],
            configuracion.GetSection("Cors:Origenes").Get<string[]>() ?? []);
    }
}
