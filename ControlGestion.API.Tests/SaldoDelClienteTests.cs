using System.Net;
using System.Net.Http.Json;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class SaldoDelClienteTests(ApiDePrueba api)
{
    private static async Task<decimal> SaldoAsync(HttpClient operador, int clienteId) =>
        (await (await operador.GetAsync($"/api/clientes/{clienteId}")).LeerJsonAsync()).GetProperty("saldoPendiente").GetDecimal();

    [Fact]
    public async Task Un_pago_parcial_baja_el_saldo_del_cliente()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var clienteId = await operador.CrearClienteAsync(zonaId);
        var cobranzaId = await operador.CrearCobranzaAsync(clienteId, zonaId, await operador.CrearProductoAsync(250m), 1);
        Assert.Equal(250m, await SaldoAsync(operador, clienteId));

        (await operador.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos", new { monto = 100, metodoPago = 0 })).EnsureSuccessStatusCode();

        Assert.Equal(150m, await SaldoAsync(operador, clienteId));
    }

    [Fact]
    public async Task Marcar_incobrable_descuenta_su_saldo_y_conserva_el_de_las_otras_cobranzas()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var clienteId = await operador.CrearClienteAsync(zonaId);
        var incobrable = await operador.CrearCobranzaAsync(clienteId, zonaId, await operador.CrearProductoAsync(23m), 1);
        await operador.CrearCobranzaAsync(clienteId, zonaId, await operador.CrearProductoAsync(10m), 1);
        Assert.Equal(33m, await SaldoAsync(operador, clienteId));

        (await operador.PostAsJsonAsync($"/api/cobranzas/{incobrable}/incobrable",
            new { observacion = "Prueba de incobrable" })).EnsureSuccessStatusCode();

        Assert.Equal(10m, await SaldoAsync(operador, clienteId));
    }

    [Fact]
    public async Task Dos_pagos_a_la_vez_en_cobranzas_distintas_del_mismo_cliente_dejan_el_saldo_correcto()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var clienteId = await operador.CrearClienteAsync(zonaId);
        var cobranzas = new[]
        {
            (Id: await operador.CrearCobranzaAsync(clienteId, zonaId, await operador.CrearProductoAsync(10m), 1), Monto: 10m),
            (Id: await operador.CrearCobranzaAsync(clienteId, zonaId, await operador.CrearProductoAsync(20m), 1), Monto: 20m)
        };

        var respuestas = await Ayudantes.EnviarALaVezAsync(2, i =>
            operador.PostAsJsonAsync($"/api/cobranzas/{cobranzas[i].Id}/pagos", new { monto = cobranzas[i].Monto, metodoPago = 0 }));

        // Si chocaron en el saldo del cliente, el segundo recibe 409 y reintenta, como haría el panel.
        for (var i = 0; i < respuestas.Length; i++)
            if (respuestas[i].StatusCode == HttpStatusCode.Conflict)
                (await operador.PostAsJsonAsync($"/api/cobranzas/{cobranzas[i].Id}/pagos",
                    new { monto = cobranzas[i].Monto, metodoPago = 0 })).EnsureSuccessStatusCode();
            else
                Assert.Equal(HttpStatusCode.OK, respuestas[i].StatusCode);

        Assert.Equal(0m, await SaldoAsync(operador, clienteId));
    }
}
