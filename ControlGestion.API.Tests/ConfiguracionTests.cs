using System.Net;
using System.Net.Http.Json;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>La configuración general (7.1): solo los días de re-visita se leen y se cambian por la API.</summary>
[Collection(ColeccionApi.Nombre)]
public class ConfiguracionTests(ApiDePrueba api)
{
    private async Task<string?> ValorEnLaBaseAsync(string clave)
    {
        using var scope = api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ConfiguracionesGenerales
            .Where(c => c.Clave == clave).Select(c => c.Valor).FirstOrDefaultAsync();
    }

    [Fact]
    public async Task La_configuracion_general_viene_sembrada_con_los_dias_del_alcance()
    {
        using var admin = await api.AdminAsync();

        var dias = await (await admin.GetAsync("/api/configuraciones/dias-revisita")).LeerJsonAsync();

        Assert.Equal(15, dias.GetProperty("dias").GetInt32());
        // El plazo del crédito no tiene pantalla (alcance, Cobranzas): solo se lee de la base.
        Assert.Equal("30", await ValorEnLaBaseAsync(ClavesDeConfiguracion.DiasVencimientoCredito));
    }

    [Fact]
    public async Task Los_dias_de_re_visita_van_de_1_a_365()
    {
        using var admin = await api.AdminAsync();

        foreach (var dias in new[] { 0, 366 })
        {
            var problema = await (await admin.PutAsJsonAsync("/api/configuraciones/dias-revisita", new { dias }))
                .ProblemaAsync(400, "validacion");
            Assert.Equal("Los días de re-visita van de 1 a 365.", problema.GetProperty("errors").GetProperty("dias")[0].GetString());
        }
        Assert.Equal("15", await ValorEnLaBaseAsync(ClavesDeConfiguracion.DiasRevisita));
    }

    [Fact]
    public async Task Solo_quien_tiene_el_permiso_lee_o_cambia_los_dias()
    {
        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);

        await (await secretaria.GetAsync("/api/configuraciones/dias-revisita")).ProblemaAsync(403, "sin_permiso");
        await (await secretaria.PutAsJsonAsync("/api/configuraciones/dias-revisita", new { dias = 20 })).ProblemaAsync(403, "sin_permiso");
    }

    [Fact]
    public async Task Las_claves_ya_no_se_borran_ni_se_crean_por_nombre()
    {
        using var admin = await api.AdminAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync("/api/configuraciones/DiasRevisita")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.PutAsJsonAsync("/api/configuraciones/OtraClave", new { clave = "OtraClave", valor = "1" })).StatusCode);
        Assert.Equal("15", await ValorEnLaBaseAsync(ClavesDeConfiguracion.DiasRevisita));
        Assert.Null(await ValorEnLaBaseAsync("OtraClave"));
    }
}
