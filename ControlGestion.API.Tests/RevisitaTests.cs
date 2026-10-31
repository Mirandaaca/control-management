using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Models;
using ControlGestion.API.Services;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlGestion.API.Tests;

/// <summary>
/// El proceso diario de re-visitas (7.3.18, ext. *b). Corre con el reloj real: con uno adelantado cambiaría también a los
/// clientes de las otras pruebas, que comparten la base. Cada prueba fija en la base la próxima visita de sus clientes.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class RevisitaTests(ApiDePrueba api)
{
    private async Task<int> ClienteConProximaVisitaAsync(HttpClient operador, int zonaId, DateTime proximaUtc, int estadoId)
    {
        var id = await operador.CrearClienteAsync(zonaId);
        using var scope = api.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Clientes.Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.UltimaVisita, proximaUtc.AddDays(-15))
                .SetProperty(c => c.ProximaVisita, proximaUtc)
                .SetProperty(c => c.EstadoClienteId, estadoId));
        return id;
    }

    private async Task<int> PasarADisponiblesAsync()
    {
        using var scope = api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RevisitaService>().PasarADisponiblesAsync();
    }

    private async Task<int> EstadoAsync(int clienteId)
    {
        using var scope = api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Clientes
            .Where(c => c.Id == clienteId).Select(c => c.EstadoClienteId).SingleAsync();
    }

    [Fact]
    public async Task Pasa_a_disponible_al_activo_cuya_proxima_visita_es_hoy_aunque_sea_mas_tarde()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var finDeHoy = HoraBolivia.FinDeHoyUtc(TimeProvider.System);
        var hoyMasTarde = await ClienteConProximaVisitaAsync(operador, zonaId, finDeHoy.AddMinutes(-1), EstadosDeCliente.Activo);
        var manana = await ClienteConProximaVisitaAsync(operador, zonaId, finDeHoy.AddMinutes(1), EstadosDeCliente.Activo);

        await PasarADisponiblesAsync();

        Assert.Equal(EstadosDeCliente.DisponibleParaRevisita, await EstadoAsync(hoyMasTarde));
        Assert.Equal(EstadosDeCliente.Activo, await EstadoAsync(manana));
    }

    [Fact]
    public async Task Un_estado_asignado_a_mano_no_cambia_aunque_su_visita_haya_llegado()
    {
        using var operador = await api.OperadorAsync();
        var inactivo = await ClienteConProximaVisitaAsync(operador, await operador.CrearZonaAsync(),
            DateTime.UtcNow.AddDays(-3), EstadosDeCliente.Inactivo);

        await PasarADisponiblesAsync();

        Assert.Equal(EstadosDeCliente.Inactivo, await EstadoAsync(inactivo));
    }

    // En el plan F1 la API se duerme y arranca varias veces al día: cada arranque corre el proceso (aviso de la 13b).
    [Fact]
    public async Task Correrlo_otra_vez_no_cambia_nada()
    {
        using var operador = await api.OperadorAsync();
        var vencido = await ClienteConProximaVisitaAsync(operador, await operador.CrearZonaAsync(),
            DateTime.UtcNow.AddDays(-2), EstadosDeCliente.Activo);

        Assert.True(await PasarADisponiblesAsync() >= 1);
        Assert.Equal(0, await PasarADisponiblesAsync());
        Assert.Equal(EstadosDeCliente.DisponibleParaRevisita, await EstadoAsync(vencido));
    }

    [Fact]
    public async Task Al_arrancar_la_API_pasa_a_disponible_sin_esperar_a_la_corrida_de_la_noche()
    {
        using var operador = await api.OperadorAsync();
        var vencido = await ClienteConProximaVisitaAsync(operador, await operador.CrearZonaAsync(),
            DateTime.UtcNow.AddDays(-1), EstadosDeCliente.Activo);
        var proceso = new ProcesoDiarioService(api.Services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
            NullLogger<ProcesoDiarioService>.Instance);

        await proceso.StartAsync(CancellationToken.None);
        var estado = EstadosDeCliente.Activo;
        for (var intento = 0; intento < 50 && estado != EstadosDeCliente.DisponibleParaRevisita; intento++)
        {
            await Task.Delay(100);
            estado = await EstadoAsync(vencido);
        }
        await proceso.StopAsync(CancellationToken.None);

        Assert.Equal(EstadosDeCliente.DisponibleParaRevisita, estado);
    }
}
