using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

/// <summary>
/// La clave de idempotencia de clientes y HP, como la de los pagos: un reintento con mala señal no duplica el registro.
/// La API guarda la clave solo junto con un registro guardado: una respuesta de error no la gasta.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class IdempotenciaTests(ApiDePrueba api)
{
    private static object Cliente(int zonaId, string telefono, Guid clave, string nombre = "Silvia Torrico Méndez",
        bool confirmarTelefonoRepetido = false) => new
    {
        nombreCompleto = nombre,
        telefono,
        direccionTexto = "Calle 6 N.º 310",
        latitud = -17.771845m,
        longitud = -63.176290m,
        zonaId,
        confirmarTelefonoRepetido,
        claveIdempotencia = clave
    };

    private static object Hoja(int clienteId, int productoId, decimal cantidad, Guid clave) => new
    {
        clienteId,
        latitud = -17.783412m,
        longitud = -63.182106m,
        tipoVenta = 1,
        detalles = new[] { new { productoId, cantidad } },
        claveIdempotencia = clave
    };

    private static string Telefono() => $"7{Random.Shared.Next(1000000, 9999999)}";

    private static async Task<int> CuantosConTelefono(HttpClient cliente, string telefono) =>
        (await (await cliente.GetAsync($"/api/clientes?buscar={telefono}")).LeerJsonAsync()).GetProperty("total").GetInt32();

    private static async Task<List<JsonElement>> HojasDe(HttpClient cliente, int clienteId) =>
        (await (await cliente.GetAsync($"/api/hojas-pedido?clienteId={clienteId}")).LeerJsonAsync())
            .GetProperty("items").EnumerateArray().ToList();

    private static async Task<decimal> StockDe(HttpClient cliente, int productoId) =>
        (await (await cliente.GetAsync($"/api/productos/{productoId}")).LeerJsonAsync()).GetProperty("stockActual").GetDecimal();

    [Fact]
    public async Task El_mismo_cliente_enviado_dos_veces_se_registra_una_sola_vez()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var telefono = Telefono();
        var clave = Guid.NewGuid();

        var primero = await operador.PostAsJsonAsync("/api/clientes", Cliente(zonaId, telefono, clave));
        // El segundo no avisa del celular repetido: es el mismo cliente.
        var segundo = await operador.PostAsJsonAsync("/api/clientes", Cliente(zonaId, telefono, clave));

        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);
        Assert.Equal(HttpStatusCode.Created, segundo.StatusCode);
        Assert.Equal((await primero.LeerJsonAsync()).GetProperty("id").GetInt32(), (await segundo.LeerJsonAsync()).GetProperty("id").GetInt32());
        Assert.Equal(1, await CuantosConTelefono(operador, telefono));
    }

    [Fact]
    public async Task Un_cliente_rechazado_no_gasta_la_clave()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var telefono = Telefono();
        await operador.CrearClienteAsync(zonaId);
        var clave = Guid.NewGuid();

        await (await operador.PostAsJsonAsync("/api/clientes", Cliente(zonaId, telefono, clave, nombre: ""))).ProblemaAsync(400);
        (await operador.PostAsJsonAsync("/api/clientes", Cliente(zonaId, telefono, Guid.NewGuid()))).EnsureSuccessStatusCode();
        // Con el celular repetido, el 409 tampoco gasta la clave: confirmarlo registra al cliente con esa misma clave.
        await (await operador.PostAsJsonAsync("/api/clientes", Cliente(zonaId, telefono, clave))).ProblemaAsync(409, "telefono_repetido");
        var registrado = await operador.PostAsJsonAsync("/api/clientes", Cliente(zonaId, telefono, clave, confirmarTelefonoRepetido: true));

        Assert.Equal(HttpStatusCode.Created, registrado.StatusCode);
        Assert.Equal(2, await CuantosConTelefono(operador, telefono));
    }

    [Fact]
    public async Task La_misma_venta_enviada_dos_veces_se_registra_y_descuenta_una_sola_vez()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var productoId = await operador.CrearProductoAsync(38m, stockInicial: 120);
        var clave = Guid.NewGuid();

        var primera = await (await operador.PostAsJsonAsync("/api/hojas-pedido/confirmadas", Hoja(clienteId, productoId, 4, clave))).LeerJsonAsync();
        var segunda = await (await operador.PostAsJsonAsync("/api/hojas-pedido/confirmadas", Hoja(clienteId, productoId, 4, clave))).LeerJsonAsync();

        Assert.Equal(primera.GetProperty("id").GetInt32(), segunda.GetProperty("id").GetInt32());
        Assert.Single(await HojasDe(operador, clienteId));
        Assert.Equal(116m, await StockDe(operador, productoId));
    }

    [Fact]
    public async Task Un_borrador_guardado_dos_veces_se_registra_una_sola_vez()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var productoId = await operador.CrearProductoAsync(10m);
        var clave = Guid.NewGuid();

        var primero = await (await operador.PostAsJsonAsync("/api/hojas-pedido", Hoja(clienteId, productoId, 1, clave))).LeerJsonAsync();
        var segundo = await (await operador.PostAsJsonAsync("/api/hojas-pedido", Hoja(clienteId, productoId, 1, clave))).LeerJsonAsync();

        Assert.Equal(primero.GetProperty("id").GetInt32(), segundo.GetProperty("id").GetInt32());
        Assert.Single(await HojasDe(operador, clienteId));
    }

    [Fact]
    public async Task Una_venta_rechazada_por_stock_no_gasta_la_clave()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var productoId = await operador.CrearProductoAsync(10m, stockInicial: 2);
        var clave = Guid.NewGuid();

        await (await operador.PostAsJsonAsync("/api/hojas-pedido/confirmadas", Hoja(clienteId, productoId, 3, clave)))
            .ProblemaAsync(400, "stock_insuficiente");
        (await operador.RegistrarIngresoAsync(productoId, 5)).EnsureSuccessStatusCode();
        var confirmada = await operador.PostAsJsonAsync("/api/hojas-pedido/confirmadas", Hoja(clienteId, productoId, 3, clave));

        Assert.Equal(HttpStatusCode.Created, confirmada.StatusCode);
        Assert.Equal(4m, await StockDe(operador, productoId));
    }

    [Fact]
    public async Task Confirmar_venta_con_la_clave_de_un_borrador_guardado_confirma_ese_borrador()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var productoId = await operador.CrearProductoAsync(10m, stockInicial: 10);
        var clave = Guid.NewGuid();
        // "Guardar" llegó a la API pero su respuesta se perdió; el promotor toca "Confirmar venta" con la misma clave.
        var guardada = await (await operador.PostAsJsonAsync("/api/hojas-pedido", Hoja(clienteId, productoId, 2, clave))).LeerJsonAsync();

        var confirmada = await (await operador.PostAsJsonAsync("/api/hojas-pedido/confirmadas", Hoja(clienteId, productoId, 2, clave)))
            .LeerJsonAsync();

        Assert.Equal(guardada.GetProperty("id").GetInt32(), confirmada.GetProperty("id").GetInt32());
        Assert.Equal(1, confirmada.GetProperty("estadoHP").GetInt32());
        Assert.Single(await HojasDe(operador, clienteId));
        Assert.Equal(8m, await StockDe(operador, productoId));
    }

    [Fact]
    public async Task Tres_envios_de_la_misma_venta_a_la_vez_descuentan_el_stock_una_vez()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var productoId = await operador.CrearProductoAsync(10m, stockInicial: 10);
        var clave = Guid.NewGuid();

        var respuestas = await Ayudantes.EnviarALaVezAsync(3, _ =>
            operador.PostAsJsonAsync("/api/hojas-pedido/confirmadas", Hoja(clienteId, productoId, 2, clave)));

        // Los que pierden la carrera devuelven la venta del primero, o chocan (409) si MySQL detecta el bloqueo.
        Assert.All(respuestas, r => Assert.Contains((int)r.StatusCode, new[] { 201, 409 }));
        Assert.Single(await HojasDe(operador, clienteId));
        Assert.Equal(8m, await StockDe(operador, productoId));
    }
}
