using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Services;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class MoraTests(ApiDePrueba api)
{
    private async Task MarcarVencidasAsync(DateTime ahoraUtc)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await new MoraService(db, new RelojFijo(new DateTimeOffset(ahoraUtc, TimeSpan.Zero))).MarcarVencidasAsync();
    }

    [Fact]
    public async Task Una_cobranza_vence_al_terminar_su_dia_en_Bolivia_y_el_cliente_no_cambia_de_estado()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var cobranzaId = await operador.CrearCobranzaAsync(clienteId, await operador.CrearProductoAsync(10m), 1);
        var cobranza = await (await operador.GetAsync($"/api/cobranzas/{cobranzaId}")).LeerJsonAsync();
        // La API devuelve la fecha en UTC, sin la "Z".
        var vencimiento = DateTime.SpecifyKind(cobranza.GetProperty("fechaVencimiento").GetDateTime(), DateTimeKind.Utc);
        var finDeSuDia = HoraBolivia.FinDelDiaUtc(DateOnly.FromDateTime(HoraBolivia.DesdeUtc(vencimiento)));
        async Task<int> EstadoAsync() =>
            (await (await operador.GetAsync($"/api/cobranzas/{cobranzaId}")).LeerJsonAsync()).GetProperty("estado").GetInt32();

        await MarcarVencidasAsync(finDeSuDia.AddMinutes(-1));
        Assert.Equal(0, await EstadoAsync());

        await MarcarVencidasAsync(finDeSuDia.AddMinutes(1));
        Assert.Equal(3, await EstadoAsync());
        // El proceso ya no le pone "Con Deuda Vencida": el estado del cliente sale solo de las visitas (5.2).
        var cliente = await (await operador.GetAsync($"/api/clientes/{clienteId}")).LeerJsonAsync();
        Assert.Equal("Activo", cliente.GetProperty("estado").GetProperty("nombre").GetString());
    }
}
