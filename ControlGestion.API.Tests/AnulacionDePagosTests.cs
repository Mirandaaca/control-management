using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class AnulacionDePagosTests(ApiDePrueba api)
{
    private record Escenario(HttpClient Operador, int ClienteId, int CobranzaId) : IDisposable
    {
        public void Dispose() => Operador.Dispose();
    }

    /// <summary>Una cobranza de 10 Bs, con un pago de 4 y otro de 6 que la dejaron pagada. Devuelve los ids de los pagos.</summary>
    private async Task<(Escenario Escenario, int PrimerPago, int PagoQueLaCerro)> CobranzaPagadaAsync()
    {
        var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var cobranzaId = await operador.CrearCobranzaAsync(clienteId, await operador.CrearProductoAsync(10m), 1);
        var primero = await PagarAsync(operador, cobranzaId, 4);
        var segundo = await PagarAsync(operador, cobranzaId, 6);
        return (new Escenario(operador, clienteId, cobranzaId), primero, segundo);
    }

    private static async Task<int> PagarAsync(HttpClient cliente, int cobranzaId, decimal monto)
    {
        var respuesta = await cliente.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos", new { monto, metodoPago = 0 });
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt32();
    }

    private static Task<HttpResponseMessage> AnularAsync(HttpClient cliente, int cobranzaId, int pagoId, string motivo = "Pago registrado dos veces") =>
        cliente.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos/{pagoId}/anular", new { motivo });

    private static async Task<decimal> SaldoAsync(HttpClient cliente, int clienteId) =>
        (await (await cliente.GetAsync($"/api/clientes/{clienteId}")).LeerJsonAsync()).GetProperty("saldoPendiente").GetDecimal();

    [Fact]
    public async Task La_secretaria_anula_el_pago_que_cerro_la_deuda_y_vuelve_a_deber()
    {
        var (escenario, _, pagoQueLaCerro) = await CobranzaPagadaAsync();
        using var alTerminar = escenario;
        Assert.Equal(0m, await SaldoAsync(escenario.Operador, escenario.ClienteId));
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        using var cliente = api.ClienteCon(secretaria.Token);

        var cobranza = await (await AnularAsync(cliente, escenario.CobranzaId, pagoQueLaCerro)).LeerJsonAsync();

        Assert.Equal(1, cobranza.GetProperty("estado").GetInt32());
        Assert.Equal(4m, cobranza.GetProperty("montoPagado").GetDecimal());
        Assert.Equal(6m, cobranza.GetProperty("saldoPendiente").GetDecimal());
        Assert.Equal(JsonValueKind.Null, cobranza.GetProperty("fechaPagoCompleto").ValueKind);
        var anulado = cobranza.GetProperty("pagos").EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == pagoQueLaCerro);
        Assert.True(anulado.GetProperty("anulado").GetBoolean());
        Assert.Equal("Pago registrado dos veces", anulado.GetProperty("motivoAnulacion").GetString());
        Assert.Equal(6m, await SaldoAsync(escenario.Operador, escenario.ClienteId));
    }

    [Fact]
    public async Task Anular_todos_los_pagos_la_deja_pendiente()
    {
        var (escenario, primero, segundo) = await CobranzaPagadaAsync();
        using var alTerminar = escenario;

        (await AnularAsync(escenario.Operador, escenario.CobranzaId, primero)).EnsureSuccessStatusCode();
        var cobranza = await (await AnularAsync(escenario.Operador, escenario.CobranzaId, segundo)).LeerJsonAsync();

        Assert.Equal(0, cobranza.GetProperty("estado").GetInt32());
        Assert.Equal(0m, cobranza.GetProperty("montoPagado").GetDecimal());
        Assert.Equal(10m, await SaldoAsync(escenario.Operador, escenario.ClienteId));
    }

    [Fact]
    public async Task Anular_sin_motivo_o_dos_veces_responde_400()
    {
        var (escenario, primero, _) = await CobranzaPagadaAsync();
        using var alTerminar = escenario;

        var sinMotivo = await (await AnularAsync(escenario.Operador, escenario.CobranzaId, primero, motivo: "")).ProblemaAsync(400, "validacion");
        Assert.True(sinMotivo.GetProperty("errors").TryGetProperty("motivo", out _));

        (await AnularAsync(escenario.Operador, escenario.CobranzaId, primero)).EnsureSuccessStatusCode();
        await (await AnularAsync(escenario.Operador, escenario.CobranzaId, primero)).ProblemaAsync(400, "pago_ya_anulado");
    }

    [Fact]
    public async Task Un_pago_de_otra_cobranza_responde_404()
    {
        var (escenario, primero, _) = await CobranzaPagadaAsync();
        using var alTerminar = escenario;
        var otra = await escenario.Operador.CrearCobranzaAsync(escenario.ClienteId, await escenario.Operador.CrearProductoAsync(5m), 1);

        await (await AnularAsync(escenario.Operador, otra, primero)).ProblemaAsync(404, "no_encontrado");
    }

    [Fact]
    public async Task El_promotor_no_anula_pagos()
    {
        var (escenario, primero, _) = await CobranzaPagadaAsync();
        using var alTerminar = escenario;
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var cliente = api.ClienteCon(promotor.Token);

        await (await AnularAsync(cliente, escenario.CobranzaId, primero)).ProblemaAsync(403, "sin_permiso");
    }

    [Fact]
    public async Task Con_sus_pagos_anulados_la_HP_ya_se_puede_anular()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var cobranzaId = await operador.CrearCobranzaAsync(clienteId, await operador.CrearProductoAsync(10m), 1);
        var hojaId = (await (await operador.GetAsync($"/api/cobranzas/{cobranzaId}")).LeerJsonAsync())
            .GetProperty("hojaPedido").GetProperty("id").GetInt32();
        var pagoId = await PagarAsync(operador, cobranzaId, 3);
        var anularHoja = () => operador.PostAsJsonAsync($"/api/hojas-pedido/{hojaId}/anular", new { motivo = "Venta duplicada" });
        await (await anularHoja()).ProblemaAsync(400, "cobranza_con_pagos");

        (await AnularAsync(operador, cobranzaId, pagoId)).EnsureSuccessStatusCode();

        (await anularHoja()).EnsureSuccessStatusCode();
        Assert.Equal(5, (await (await operador.GetAsync($"/api/cobranzas/{cobranzaId}")).LeerJsonAsync()).GetProperty("estado").GetInt32());
        Assert.Equal(0m, await SaldoAsync(operador, clienteId));
    }
}
