using System.Net;
using System.Net.Http.Json;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Services;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlGestion.API.Tests;

/// <summary>Lo que la API necesita para correr en Azure App Service (tarea 13b).</summary>
[Collection(ColeccionApi.Nombre)]
public class DespliegueTests(ApiDePrueba api)
{
    [Fact]
    public async Task Health_responde_sin_sesion_y_sin_detalles()
    {
        using var cliente = api.CreateClient();

        var respuesta = await cliente.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("Healthy", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public void La_zona_de_Bolivia_y_los_montos_estan_en_el_sistema()
    {
        Assert.Null(ComprobacionRegional.Problema());
    }

    [Fact]
    public void Sin_la_zona_o_con_otros_montos_la_comprobacion_dice_que_falta()
    {
        Assert.StartsWith("No existe la zona horaria 'America/No_Existe'",
            ComprobacionRegional.Problema(zona: "America/No_Existe"));
        Assert.StartsWith("Los montos salen 'Bs 1,234.50'", ComprobacionRegional.Problema(cultura: "en-US"));
    }

    // App Service atiende el https y le pasa la petición a la API por http. En Azure lo activa la variable
    // ASPNETCORE_FORWARDEDHEADERS_ENABLED; aquí, la misma opción.
    [Fact]
    public async Task Detras_del_proxy_la_API_ve_el_https_y_la_IP_de_quien_pide()
    {
        await using var detrasDelProxy = api.WithWebHostBuilder(b => b.UseSetting("FORWARDEDHEADERS_ENABLED", "true"));
        using var operador = detrasDelProxy.ClienteCon(
            await detrasDelProxy.IniciarSesionAsync(ApiDePrueba.CorreoOperador, api.ContrasenaOperador));
        var ip = $"203.0.113.{Random.Shared.Next(1, 255)}";
        var sku = Ayudantes.Unico("P");
        var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/productos")
        {
            Content = JsonContent.Create(new { sku, descripcion = $"Producto {sku}", precio = 10m, precioCompra = 10m, categoria = "Pruebas" })
        };
        pedido.Headers.Add("X-Forwarded-Proto", "https");
        pedido.Headers.Add("X-Forwarded-For", ip);

        var respuesta = await operador.SendAsync(pedido);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Equal("https", respuesta.Headers.Location?.Scheme);
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.AuditoriaLogs.AnyAsync(l => l.Modulo == "Parámetros y Configuraciones" && l.DireccionIp == ip));
    }

    // En el plan F1 la API se duerme: al despertar, el proceso arranca de nuevo. Las vencidas se marcan al arrancar, sin
    // esperar a las 00:05.
    [Fact]
    public async Task Al_arrancar_la_API_marca_las_vencidas_sin_esperar_a_la_corrida_de_la_noche()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var cobranzaId = await operador.CrearCobranzaAsync(clienteId, await operador.CrearProductoAsync(10m), 1);
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Cobranzas.Where(c => c.Id == cobranzaId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.FechaVencimiento, DateTime.UtcNow.AddDays(-2)));
        }
        var proceso = new MoraDetectionService(api.Services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
            NullLogger<MoraDetectionService>.Instance);

        await proceso.StartAsync(CancellationToken.None);
        var estado = 0;
        for (var intento = 0; intento < 50 && estado != 3; intento++)
        {
            await Task.Delay(100);
            estado = (await (await operador.GetAsync($"/api/cobranzas/{cobranzaId}")).LeerJsonAsync()).GetProperty("estado").GetInt32();
        }
        await proceso.StopAsync(CancellationToken.None);

        Assert.Equal(3, estado);
    }
}
