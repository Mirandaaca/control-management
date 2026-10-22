using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

/// <summary>"Confirmar venta" en un formulario nuevo (7.3.5): registrar y confirmar la HP en un solo paso.</summary>
[Collection(ColeccionApi.Nombre)]
public class HojasPedidoConfirmadasTests(ApiDePrueba api)
{
    private static Task<HttpResponseMessage> ConfirmarVentaAsync(HttpClient cliente, int clienteId, int productoId,
        decimal cantidad, bool credito = true) =>
        cliente.PostAsJsonAsync("/api/hojas-pedido/confirmadas", new
        {
            clienteId,
            latitud = -17.783412m,
            longitud = -63.182106m,
            precisionGPS = 8f,
            tipoVenta = credito ? 1 : 0,
            detalles = new[] { new { productoId, cantidad } }
        });

    private static async Task<decimal> StockDe(HttpClient cliente, int productoId) =>
        (await (await cliente.GetAsync($"/api/productos/{productoId}")).LeerJsonAsync()).GetProperty("stockActual").GetDecimal();

    private static async Task<List<JsonElement>> HojasDe(HttpClient cliente, int clienteId) =>
        (await (await cliente.GetAsync($"/api/hojas-pedido?clienteId={clienteId}")).LeerJsonAsync())
            .GetProperty("items").EnumerateArray().ToList();

    // El número correlativo de "HP-2026-00018": 18.
    private static int Correlativo(JsonElement hoja) => int.Parse(hoja.GetProperty("numero").GetString()!.Split('-')[^1]);

    [Fact]
    public async Task El_promotor_confirma_la_venta_sin_guardarla_antes_y_nace_su_deuda()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id));
        var productoId = await operador.CrearProductoAsync(38m, stockInicial: 120);
        using var cliente = api.ClienteCon(promotor.Token);

        var respuesta = await ConfirmarVentaAsync(cliente, clienteId, productoId, 4);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var hoja = await respuesta.LeerJsonAsync();
        Assert.Equal(1, hoja.GetProperty("estadoHP").GetInt32());
        Assert.Equal(152m, hoja.GetProperty("total").GetDecimal());
        Assert.Equal(8f, hoja.GetProperty("precisionGPS").GetSingle());
        Assert.Equal(promotor.Id, hoja.GetProperty("promotor").GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Object, hoja.GetProperty("cobranza").ValueKind);
        Assert.Equal(116m, await StockDe(operador, productoId));
    }

    [Fact]
    public async Task Sin_stock_no_queda_nada_ni_el_borrador_ni_su_numero()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var productoId = await operador.CrearProductoAsync(10m, stockInicial: 2);
        var antes = await (await operador.GetAsync($"/api/hojas-pedido/{await operador.CrearHojaPedidoAsync(clienteId, productoId, 1)}"))
            .LeerJsonAsync();

        var problema = await (await ConfirmarVentaAsync(operador, clienteId, productoId, 3)).ProblemaAsync(400, "stock_insuficiente");

        Assert.Contains("hay 2 y la venta pide 3", problema.GetProperty("errors").GetProperty("detalles[0].cantidad")[0].GetString());
        Assert.Single(await HojasDe(operador, clienteId));
        Assert.Equal(2m, await StockDe(operador, productoId));
        Assert.Empty((await (await operador.GetAsync($"/api/cobranzas?clienteId={clienteId}")).LeerJsonAsync()).GetProperty("items").EnumerateArray());
        // La venta rechazada no gastó un número: la siguiente HP lleva el que sigue al de antes.
        var despues = await (await operador.GetAsync($"/api/hojas-pedido/{await operador.CrearHojaPedidoAsync(clienteId, productoId, 1)}"))
            .LeerJsonAsync();
        Assert.Equal(Correlativo(antes) + 1, Correlativo(despues));
    }

    [Fact]
    public async Task Confirmar_venta_pide_registrar_y_confirmar_HP()
    {
        using var operador = await api.OperadorAsync();
        using var admin = await api.AdminAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var productoId = await operador.CrearProductoAsync(10m);
        var (_, soloRegistra) = await admin.CrearRolAsync(Permisos.Ventas.RegistrarHp, Permisos.Seguridad.VerCarteraCompleta);
        var (_, registraYConfirma) = await admin.CrearRolAsync(Permisos.Ventas.RegistrarHp, Permisos.Ventas.ConfirmarHp,
            Permisos.Seguridad.VerCarteraCompleta);
        using var conUno = api.ClienteCon((await api.CrearUsuarioAsync(soloRegistra)).Token);
        using var conLosDos = api.ClienteCon((await api.CrearUsuarioAsync(registraYConfirma)).Token);

        await (await ConfirmarVentaAsync(conUno, clienteId, productoId, 1)).ProblemaAsync(403);
        Assert.Equal(HttpStatusCode.Created, (await ConfirmarVentaAsync(conLosDos, clienteId, productoId, 1)).StatusCode);
    }
}
