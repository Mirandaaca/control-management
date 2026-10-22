using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Services;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>Las deudas vencidas (3.2): sus días de mora y su orden.</summary>
[Collection(ColeccionApi.Nombre)]
public class DeudasVencidasTests(ApiDePrueba api)
{
    private async Task VencerAsync(int cobranzaId, DateTime vencimientoUtc, RelojFijo reloj)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Cobranzas.Where(c => c.Id == cobranzaId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.FechaVencimiento, vencimientoUtc));
        await new MoraService(db, reloj).MarcarVencidasAsync();
    }

    [Fact]
    public async Task Cada_vencida_trae_sus_dias_de_mora_en_dias_de_Bolivia_y_la_mas_atrasada_va_primero()
    {
        // La hora real, fija: así la cuenta no cambia si la prueba cruza la medianoche, y la sesión sigue valiendo.
        var reloj = new RelojFijo(DateTimeOffset.UtcNow);
        var hoy = HoraBolivia.Hoy(reloj);
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var productoId = await operador.CrearProductoAsync(10m);
        var masAtrasada = await operador.CrearCobranzaAsync(clienteId, productoId, 1);
        var reciente = await operador.CrearCobranzaAsync(clienteId, productoId, 1);
        var alDia = await operador.CrearCobranzaAsync(clienteId, productoId, 1);
        // Venció hace tres días a las 23:30 de Bolivia: son menos de 72 horas, pero tres días de calendario.
        await VencerAsync(masAtrasada, HoraBolivia.InicioDelDiaUtc(hoy.AddDays(-3)).AddMinutes(23 * 60 + 30), reloj);
        await VencerAsync(reciente, HoraBolivia.InicioDelDiaUtc(hoy.AddDays(-1)).AddMinutes(10), reloj);
        await using var fabrica = api.ConReloj(reloj);
        using var cliente = fabrica.ClienteCon(await fabrica.IniciarSesionAsync(ApiDePrueba.CorreoOperador, api.ContrasenaOperador));

        var vencidas = (await (await cliente.GetAsync($"/api/cobranzas?clienteId={clienteId}&estado=3")).LeerJsonAsync())
            .GetProperty("items").EnumerateArray()
            .Select(c => (c.GetProperty("id").GetInt32(), c.GetProperty("diasDeMora").GetInt32())).ToList();
        Assert.Equal([(masAtrasada, 3), (reciente, 1)], vencidas);

        var detalle = await (await cliente.GetAsync($"/api/cobranzas/{masAtrasada}")).LeerJsonAsync();
        Assert.Equal(3, detalle.GetProperty("diasDeMora").GetInt32());
        // Una cobranza que no está vencida no tiene mora: el campo va vacío, no en cero.
        var pendiente = await (await cliente.GetAsync($"/api/cobranzas/{alDia}")).LeerJsonAsync();
        Assert.Equal(System.Text.Json.JsonValueKind.Null, pendiente.GetProperty("diasDeMora").ValueKind);
    }
}
