using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class PagosTests(ApiDePrueba api)
{
    /// <summary>Crea una cobranza nueva de 1 × precio para un cliente con crédito.</summary>
    private async Task<(HttpClient Operador, int CobranzaId)> CobranzaDeAsync(decimal precio)
    {
        var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var productoId = await operador.CrearProductoAsync(precio);
        var clienteId = await operador.CrearClienteAsync(zonaId);
        return (operador, await operador.CrearCobranzaAsync(clienteId, productoId, cantidad: 1));
    }

    private static async Task<JsonElement> CobranzaAsync(HttpClient cliente, int id) =>
        await (await cliente.GetAsync($"/api/cobranzas/{id}")).LeerJsonAsync();

    private static object Pago(decimal monto, Guid? clave = null) => new { monto, metodoPago = 0, claveIdempotencia = clave };

    [Fact]
    public async Task Cinco_pagos_a_la_vez_sobre_la_misma_deuda_registran_uno_solo()
    {
        var (operador, cobranzaId) = await CobranzaDeAsync(8m);
        using var _ = operador;

        var respuestas = await Ayudantes.EnviarALaVezAsync(5, _ =>
            operador.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos", Pago(8m)));

        // Los que perdieron la carrera reciben 409 (chocaron) o 400 (llegaron cuando ya estaba pagada).
        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(respuestas.Where(r => r.StatusCode != HttpStatusCode.OK),
            r => Assert.Contains((int)r.StatusCode, new[] { 400, 409 }));
        var cobranza = await CobranzaAsync(operador, cobranzaId);
        Assert.Equal(8m, cobranza.GetProperty("montoPagado").GetDecimal());
        Assert.Equal(1, cobranza.GetProperty("pagos").GetArrayLength());
    }

    [Fact]
    public async Task Un_monto_mayor_que_el_saldo_dice_el_saldo_en_Bs_en_el_campo_del_monto()
    {
        var (operador, cobranzaId) = await CobranzaDeAsync(1234.5m);
        using var _ = operador;

        var problema = await (await operador.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos", Pago(1300m)))
            .ProblemaAsync(400, "monto_invalido");

        const string mensaje = "El monto no puede superar el saldo (Bs 1.234,50).";
        Assert.Equal(mensaje, problema.GetProperty("detail").GetString());
        Assert.Equal(mensaje, problema.GetProperty("errors").GetProperty("monto")[0].GetString());
        Assert.Equal(0m, (await CobranzaAsync(operador, cobranzaId)).GetProperty("montoPagado").GetDecimal());
    }

    [Fact]
    public async Task Repetir_un_pago_con_la_misma_clave_devuelve_el_mismo_pago()
    {
        var (operador, cobranzaId) = await CobranzaDeAsync(20m);
        using var _ = operador;
        var clave = Guid.NewGuid();

        var primero = await (await operador.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos", Pago(5m, clave))).LeerJsonAsync();
        var segundo = await (await operador.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos", Pago(5m, clave))).LeerJsonAsync();

        Assert.Equal(primero.GetProperty("id").GetInt32(), segundo.GetProperty("id").GetInt32());
        var cobranza = await CobranzaAsync(operador, cobranzaId);
        Assert.Equal(5m, cobranza.GetProperty("montoPagado").GetDecimal());
        Assert.Equal(1, cobranza.GetProperty("pagos").GetArrayLength());
    }

    [Fact]
    public async Task Cinco_envios_a_la_vez_con_la_misma_clave_crean_un_solo_pago()
    {
        var (operador, cobranzaId) = await CobranzaDeAsync(20m);
        using var _ = operador;
        var clave = Guid.NewGuid();

        var respuestas = await Ayudantes.EnviarALaVezAsync(5, _ =>
            operador.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos", Pago(3m, clave)));

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var ids = await Task.WhenAll(respuestas.Select(async r => (await r.LeerJsonAsync()).GetProperty("id").GetInt32()));
        Assert.Single(ids.Distinct());
        Assert.Equal(1, (await CobranzaAsync(operador, cobranzaId)).GetProperty("pagos").GetArrayLength());
    }

    [Fact]
    public async Task La_clave_de_otra_cobranza_responde_409()
    {
        var (operador, primeraId) = await CobranzaDeAsync(20m);
        using var _ = operador;
        var (otroOperador, segundaId) = await CobranzaDeAsync(20m);
        otroOperador.Dispose();
        var clave = Guid.NewGuid();
        (await operador.PostAsJsonAsync($"/api/cobranzas/{primeraId}/pagos", Pago(1m, clave))).EnsureSuccessStatusCode();

        await (await operador.PostAsJsonAsync($"/api/cobranzas/{segundaId}/pagos", Pago(1m, clave)))
            .ProblemaAsync(409, "clave_idempotencia_usada");
    }

    [Fact]
    public async Task Un_pago_y_la_marca_de_incobrable_a_la_vez_no_pierden_cambios()
    {
        var (operador, cobranzaId) = await CobranzaDeAsync(8m);
        using var _ = operador;

        var respuestas = await Ayudantes.EnviarALaVezAsync(2, i => i == 0
            ? operador.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos", Pago(3m))
            : operador.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/incobrable", new { observacion = "Prueba de choque" }));

        var cobranza = await CobranzaAsync(operador, cobranzaId);
        var pagos = cobranza.GetProperty("pagos").EnumerateArray().ToList();
        // Lo que quedó guardado coincide con lo que se respondió: nadie recibió 200 por un cambio perdido.
        Assert.Equal(pagos.Sum(p => p.GetProperty("monto").GetDecimal()), cobranza.GetProperty("montoPagado").GetDecimal());
        Assert.Equal(respuestas[0].StatusCode == HttpStatusCode.OK ? 1 : 0, pagos.Count);
        if (respuestas[1].StatusCode == HttpStatusCode.OK)
            Assert.Equal(4, cobranza.GetProperty("estado").GetInt32());
    }
}
