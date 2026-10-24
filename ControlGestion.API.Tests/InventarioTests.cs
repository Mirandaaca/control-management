using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class InventarioTests(ApiDePrueba api)
{
    private static async Task<int> CrearProductoAsync(HttpClient operador, string sku, decimal stockMinimo = 0)
    {
        var respuesta = await operador.PostAsJsonAsync("/api/productos",
            new { sku, descripcion = $"Producto {sku}", precio = 10m, precioCompra = 7m, stockMinimo });
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt32();
    }

    private static async Task<JsonElement> PaginaAsync(HttpClient cliente, string ruta) =>
        await (await cliente.GetAsync(ruta)).LeerJsonAsync();

    [Fact]
    public async Task Un_producto_nuevo_aparece_en_el_inventario_con_stock_cero_y_bajo_el_minimo()
    {
        using var operador = await api.OperadorAsync();
        var sku = Ayudantes.Unico("INV");
        var productoId = await CrearProductoAsync(operador, sku);

        var pagina = await PaginaAsync(operador, $"/api/inventario?buscar={sku}");

        Assert.Equal(1, pagina.GetProperty("total").GetInt32());
        var item = pagina.GetProperty("items")[0];
        Assert.Equal(productoId, item.GetProperty("productoId").GetInt32());
        Assert.Equal(0m, item.GetProperty("stockActual").GetDecimal());
        // La secretaria ve el precio de venta al buscar productos (7.3.7, paso 2.2).
        Assert.Equal(10m, item.GetProperty("precioVenta").GetDecimal());
        // Un stock igual al mínimo ya cuenta como bajo el mínimo.
        Assert.True(item.GetProperty("bajoMinimo").GetBoolean());
    }

    [Fact]
    public async Task El_filtro_bajo_el_minimo_trae_solo_los_que_estan_en_el_minimo_o_debajo()
    {
        using var operador = await api.OperadorAsync();
        var prefijo = Ayudantes.Unico("INV");
        var conStock = await CrearProductoAsync(operador, $"{prefijo}-A", stockMinimo: 5);
        var sinStock = await CrearProductoAsync(operador, $"{prefijo}-B", stockMinimo: 5);
        (await operador.RegistrarIngresoAsync(conStock, 10)).EnsureSuccessStatusCode();
        (await operador.RegistrarIngresoAsync(sinStock, 2)).EnsureSuccessStatusCode();

        var pagina = await PaginaAsync(operador, $"/api/inventario?buscar={prefijo}&soloBajoMinimo=true");

        var item = Assert.Single(pagina.GetProperty("items").EnumerateArray());
        Assert.Equal(sinStock, item.GetProperty("productoId").GetInt32());
    }

    [Fact]
    public async Task El_kardex_viene_paginado_del_mas_reciente_al_mas_antiguo()
    {
        using var operador = await api.OperadorAsync();
        var productoId = await CrearProductoAsync(operador, Ayudantes.Unico("INV"));
        foreach (var cantidad in new[] { 1m, 2m, 3m })
            (await operador.RegistrarIngresoAsync(productoId, cantidad)).EnsureSuccessStatusCode();

        var pagina = await PaginaAsync(operador, $"/api/inventario/{productoId}/movimientos?tamano=2");

        Assert.Equal(3, pagina.GetProperty("total").GetInt32());
        Assert.Equal(2, pagina.GetProperty("totalPaginas").GetInt32());
        var masReciente = pagina.GetProperty("items")[0];
        Assert.Equal(3m, masReciente.GetProperty("variacion").GetDecimal());
        Assert.Equal(6m, masReciente.GetProperty("stockResultante").GetDecimal());
        Assert.Equal("Operador de pruebas", masReciente.GetProperty("usuario").GetProperty("nombreCompleto").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Un_ingreso_sin_cantidad_positiva_responde_400(decimal cantidad)
    {
        using var operador = await api.OperadorAsync();
        var productoId = await CrearProductoAsync(operador, Ayudantes.Unico("INV"));

        var problema = await (await operador.RegistrarIngresoAsync(productoId, cantidad)).ProblemaAsync(400, "validacion");

        Assert.True(problema.GetProperty("errors").TryGetProperty("cantidad", out _));
    }

    [Fact]
    public async Task Un_ingreso_queda_en_el_kardex_como_entrada()
    {
        using var operador = await api.OperadorAsync();
        var productoId = await CrearProductoAsync(operador, Ayudantes.Unico("INV"));

        var inventario = await (await operador.RegistrarIngresoAsync(productoId, 12)).LeerJsonAsync();

        Assert.Equal(12m, inventario.GetProperty("stockActual").GetDecimal());
        var movimiento = (await PaginaAsync(operador, $"/api/inventario/{productoId}/movimientos")).GetProperty("items")[0];
        Assert.Equal(0, movimiento.GetProperty("tipoMovimiento").GetInt32());
        Assert.Equal(12m, movimiento.GetProperty("variacion").GetDecimal());
        Assert.Equal("Ingreso de la prueba", movimiento.GetProperty("observacion").GetString());
    }

    [Fact]
    public async Task Diez_ingresos_a_la_vez_nunca_responden_500_ni_pierden_unidades()
    {
        using var operador = await api.OperadorAsync();
        var productoId = await CrearProductoAsync(operador, Ayudantes.Unico("INV"));

        var respuestas = await Ayudantes.EnviarALaVezAsync(10, _ => operador.RegistrarIngresoAsync(productoId, 1));

        // Los que chocan con otro ingreso responden 409 y no suman: el stock es exactamente la cantidad de 200.
        Assert.All(respuestas, r => Assert.Contains((int)r.StatusCode, new[] { 200, 409 }));
        var aceptados = respuestas.Count(r => (int)r.StatusCode == 200);
        var producto = await (await operador.GetAsync($"/api/productos/{productoId}")).LeerJsonAsync();
        Assert.Equal(aceptados, producto.GetProperty("stockActual").GetDecimal());
    }

    [Fact]
    public async Task El_promotor_no_registra_ingresos()
    {
        using var operador = await api.OperadorAsync();
        var productoId = await CrearProductoAsync(operador, Ayudantes.Unico("INV"));
        var promotor = await api.CrearUsuarioAsync(ControlGestion.API.Seguridad.Roles.Promotor);
        using var cliente = api.ClienteCon(promotor.Token);

        await (await cliente.RegistrarIngresoAsync(productoId, 5)).ProblemaAsync(403, "sin_permiso");
    }

    [Fact]
    public async Task El_kardex_de_un_producto_que_no_existe_responde_404()
    {
        using var operador = await api.OperadorAsync();

        await (await operador.GetAsync("/api/inventario/999999/movimientos")).ProblemaAsync(404, "no_encontrado");
    }
}
