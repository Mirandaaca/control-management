using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class PagosTests(ApiDePrueba api)
{
    /// <summary>Crea una cobranza nueva de 1 × precio para un cliente con crédito.</summary>
    private async Task<(HttpClient Admin, int CobranzaId)> CobranzaDeAsync(decimal precio)
    {
        var admin = await api.AdminAsync();
        var zonaId = await admin.CrearZonaAsync();
        var productoId = await admin.CrearProductoAsync(precio);
        var clienteId = await admin.CrearClienteAsync(zonaId);
        return (admin, await admin.CrearCobranzaAsync(clienteId, zonaId, productoId, cantidad: 1));
    }

    private static async Task<JsonElement> CobranzaAsync(HttpClient cliente, int id) =>
        await (await cliente.GetAsync($"/api/cobranzas/{id}")).LeerJsonAsync();

    private static object Pago(decimal monto, Guid? clave = null) => new { monto, metodoPago = 0, claveIdempotencia = clave };

    [Fact]
    public async Task Cinco_pagos_a_la_vez_sobre_la_misma_deuda_registran_uno_solo()
    {
        var (admin, cobranzaId) = await CobranzaDeAsync(8m);
        using var _ = admin;

        var respuestas = await Ayudantes.EnviarALaVezAsync(5, _ =>
            admin.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos", Pago(8m)));

        // Los que perdieron la carrera reciben 409 (chocaron) o 400 (llegaron cuando ya estaba pagada).
        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(respuestas.Where(r => r.StatusCode != HttpStatusCode.OK),
            r => Assert.Contains((int)r.StatusCode, new[] { 400, 409 }));
        var cobranza = await CobranzaAsync(admin, cobranzaId);
        Assert.Equal(8m, cobranza.GetProperty("montoPagado").GetDecimal());
        Assert.Equal(1, cobranza.GetProperty("pagos").GetArrayLength());
    }

    [Fact]
    public async Task Repetir_un_pago_con_la_misma_clave_devuelve_el_mismo_pago()
    {
        var (admin, cobranzaId) = await CobranzaDeAsync(20m);
        using var _ = admin;
        var clave = Guid.NewGuid();

        var primero = await (await admin.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos", Pago(5m, clave))).LeerJsonAsync();
        var segundo = await (await admin.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos", Pago(5m, clave))).LeerJsonAsync();

        Assert.Equal(primero.GetProperty("id").GetInt32(), segundo.GetProperty("id").GetInt32());
        var cobranza = await CobranzaAsync(admin, cobranzaId);
        Assert.Equal(5m, cobranza.GetProperty("montoPagado").GetDecimal());
        Assert.Equal(1, cobranza.GetProperty("pagos").GetArrayLength());
    }

    [Fact]
    public async Task Cinco_envios_a_la_vez_con_la_misma_clave_crean_un_solo_pago()
    {
        var (admin, cobranzaId) = await CobranzaDeAsync(20m);
        using var _ = admin;
        var clave = Guid.NewGuid();

        var respuestas = await Ayudantes.EnviarALaVezAsync(5, _ =>
            admin.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos", Pago(3m, clave)));

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var ids = await Task.WhenAll(respuestas.Select(async r => (await r.LeerJsonAsync()).GetProperty("id").GetInt32()));
        Assert.Single(ids.Distinct());
        Assert.Equal(1, (await CobranzaAsync(admin, cobranzaId)).GetProperty("pagos").GetArrayLength());
    }

    [Fact]
    public async Task La_clave_de_otra_cobranza_responde_409()
    {
        var (admin, primeraId) = await CobranzaDeAsync(20m);
        using var _ = admin;
        var (otroAdmin, segundaId) = await CobranzaDeAsync(20m);
        otroAdmin.Dispose();
        var clave = Guid.NewGuid();
        (await admin.PostAsJsonAsync($"/api/cobranzas/{primeraId}/pagos", Pago(1m, clave))).EnsureSuccessStatusCode();

        await (await admin.PostAsJsonAsync($"/api/cobranzas/{segundaId}/pagos", Pago(1m, clave)))
            .ProblemaAsync(409, "clave_idempotencia_usada");
    }

    [Fact]
    public async Task Un_pago_y_la_marca_de_incobrable_a_la_vez_no_pierden_cambios()
    {
        var (admin, cobranzaId) = await CobranzaDeAsync(8m);
        using var _ = admin;

        var respuestas = await Ayudantes.EnviarALaVezAsync(2, i => i == 0
            ? admin.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos", Pago(3m))
            : admin.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/incobrable", "Prueba de choque"));

        var cobranza = await CobranzaAsync(admin, cobranzaId);
        var pagos = cobranza.GetProperty("pagos").EnumerateArray().ToList();
        // Lo que quedó guardado coincide con lo que se respondió: nadie recibió 200 por un cambio perdido.
        Assert.Equal(pagos.Sum(p => p.GetProperty("monto").GetDecimal()), cobranza.GetProperty("montoPagado").GetDecimal());
        Assert.Equal(respuestas[0].StatusCode == HttpStatusCode.OK ? 1 : 0, pagos.Count);
        if (respuestas[1].StatusCode == HttpStatusCode.OK)
            Assert.Equal(4, cobranza.GetProperty("estado").GetInt32());
    }
}
