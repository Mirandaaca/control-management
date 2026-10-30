using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Models;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>
/// Cambiar los días de re-visita (7.1) recalcula la próxima visita y el estado de los clientes, con la regla "llegó" por
/// día de Bolivia. La configuración es de toda la base de pruebas: cada prueba la deja otra vez en 15.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class DiasRevisitaTests(ApiDePrueba api)
{
    // Sin fracciones de segundo: MySQL guarda microsegundos y así las fechas se comparan exactas.
    private static readonly DateTimeOffset Ahora = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    private sealed record Sesion(WebApplicationFactory<Program> Fabrica, HttpClient Cliente) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Cliente.Dispose();
            await Fabrica.DisposeAsync();
        }
    }

    /// <summary>La API con el reloj fijo en <see cref="Ahora"/> y la sesión del operador.</summary>
    private async Task<Sesion> ConRelojFijoAsync()
    {
        var fabrica = api.ConReloj(new RelojFijo(Ahora));
        return new Sesion(fabrica, fabrica.ClienteCon(await fabrica.IniciarSesionAsync(ApiDePrueba.CorreoOperador, api.ContrasenaOperador)));
    }

    /// <summary>Un cliente nuevo con su última visita y su estado fijados en la base, con la próxima a los 15 días.</summary>
    private async Task<int> ClienteVisitadoAsync(HttpClient operador, DateTime ultimaVisitaUtc, int estadoId, int? zonaId = null)
    {
        var id = await operador.CrearClienteAsync(zonaId ?? await operador.CrearZonaAsync());
        using var scope = api.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Clientes.Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.UltimaVisita, ultimaVisitaUtc)
                .SetProperty(c => c.ProximaVisita, ultimaVisitaUtc.AddDays(15))
                .SetProperty(c => c.EstadoClienteId, estadoId));
        return id;
    }

    private static async Task<JsonElement> CambiarDiasAsync(HttpClient cliente, int dias)
    {
        var respuesta = await cliente.PutAsJsonAsync("/api/configuraciones/dias-revisita", new { dias });
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.LeerJsonAsync();
    }

    private static async Task<(string Estado, DateTime? Proxima)> ClienteAsync(HttpClient cliente, int id)
    {
        var json = await (await cliente.GetAsync($"/api/clientes/{id}")).LeerJsonAsync();
        var proxima = json.GetProperty("proximaVisita");
        return (json.GetProperty("estado").GetProperty("nombre").GetString()!,
            proxima.ValueKind == JsonValueKind.Null ? null : DateTime.SpecifyKind(proxima.GetDateTime(), DateTimeKind.Utc));
    }

    [Fact]
    public async Task Bajar_y_subir_los_dias_mueve_la_proxima_visita_y_el_estado_en_las_dos_direcciones()
    {
        await using var sesion = await ConRelojFijoAsync();
        var hoy = HoraBolivia.Hoy(new RelojFijo(Ahora));
        // Visitado hace 10 días a las 11:00 de Bolivia: con 15 días sigue activo.
        var ultima = HoraBolivia.InicioDelDiaUtc(hoy.AddDays(-10)).AddHours(11);
        var id = await ClienteVisitadoAsync(sesion.Cliente, ultima, EstadosDeCliente.Activo);
        try
        {
            var conOcho = await CambiarDiasAsync(sesion.Cliente, 8);
            Assert.Equal(("Disponible para re-visita", ultima.AddDays(8)), await ClienteAsync(sesion.Cliente, id));
            Assert.Equal(8, conOcho.GetProperty("dias").GetInt32());
            Assert.True(conOcho.GetProperty("pasaronADisponible").GetInt32() >= 1);
            Assert.True(conOcho.GetProperty("clientesRecalculados").GetInt32() >= 1);

            var conVeinte = await CambiarDiasAsync(sesion.Cliente, 20);
            Assert.Equal(("Activo", ultima.AddDays(20)), await ClienteAsync(sesion.Cliente, id));
            Assert.True(conVeinte.GetProperty("pasaronAActivo").GetInt32() >= 1);
        }
        finally
        {
            await CambiarDiasAsync(sesion.Cliente, 15);
        }
    }

    [Fact]
    public async Task Inactivo_recibe_la_fecha_nueva_sin_cambiar_de_estado_y_un_cliente_sin_visitas_no_cambia()
    {
        await using var sesion = await ConRelojFijoAsync();
        var ultima = HoraBolivia.InicioDelDiaUtc(HoraBolivia.Hoy(new RelojFijo(Ahora)).AddDays(-30)).AddHours(11);
        var inactivo = await ClienteVisitadoAsync(sesion.Cliente, ultima, EstadosDeCliente.Inactivo);
        var sinVisitas = await sesion.Cliente.CrearClienteAsync(await sesion.Cliente.CrearZonaAsync());
        try
        {
            await CambiarDiasAsync(sesion.Cliente, 10);

            Assert.Equal(("Inactivo", ultima.AddDays(10)), await ClienteAsync(sesion.Cliente, inactivo));
            Assert.Equal(("Disponible para re-visita", (DateTime?)null), await ClienteAsync(sesion.Cliente, sinVisitas));
        }
        finally
        {
            await CambiarDiasAsync(sesion.Cliente, 15);
        }
    }

    [Fact]
    public async Task Una_visita_que_toca_hoy_mas_tarde_ya_llego_al_recalcular_en_por_visitar_y_al_volver_al_ciclo()
    {
        await using var sesion = await ConRelojFijoAsync();
        // Más tarde, pero todavía hoy en Bolivia: a mitad de camino entre ahora y la medianoche.
        var finDeHoy = HoraBolivia.FinDeHoyUtc(new RelojFijo(Ahora));
        var masTarde = Ahora.UtcDateTime.AddSeconds(Math.Floor((finDeHoy - Ahora.UtcDateTime).TotalSeconds / 2));
        var zonaId = await sesion.Cliente.CrearZonaAsync();
        var id = await ClienteVisitadoAsync(sesion.Cliente, masTarde.AddDays(-15), EstadosDeCliente.Activo, zonaId);
        try
        {
            await CambiarDiasAsync(sesion.Cliente, 15);
            Assert.Equal(("Disponible para re-visita", masTarde), await ClienteAsync(sesion.Cliente, id));

            var porVisitar = (await (await sesion.Cliente.GetAsync($"/api/clientes?porVisitar=true&zonaId={zonaId}")).LeerJsonAsync())
                .GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetInt32());
            Assert.Equal([id], porVisitar);

            (await sesion.Cliente.PutAsJsonAsync($"/api/clientes/{id}/estado", new { estadoClienteId = EstadosDeCliente.Inactivo }))
                .EnsureSuccessStatusCode();
            var deVuelta = await (await sesion.Cliente.PutAsJsonAsync($"/api/clientes/{id}/estado", new { estadoClienteId = (int?)null }))
                .LeerJsonAsync();
            Assert.Equal("Disponible para re-visita", deVuelta.GetProperty("estado").GetProperty("nombre").GetString());
        }
        finally
        {
            await CambiarDiasAsync(sesion.Cliente, 15);
        }
    }
}
