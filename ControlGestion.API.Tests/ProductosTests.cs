using System.Net;
using System.Net.Http.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class ProductosTests(ApiDePrueba api)
{
    private static object Producto(string sku, decimal precio = 10m, decimal precioCompra = 7m, decimal stockMinimo = 0m) =>
        new { sku, descripcion = $"Producto {sku}", categoria = "Pruebas", precio, precioCompra, stockMinimo };

    [Fact]
    public async Task Un_SKU_repetido_responde_409()
    {
        using var operador = await api.OperadorAsync();
        var sku = Ayudantes.Unico("SKU");
        (await operador.PostAsJsonAsync("/api/productos", Producto(sku))).EnsureSuccessStatusCode();

        var problema = await (await operador.PostAsJsonAsync("/api/productos", Producto(sku))).ProblemaAsync(409, "sku_repetido");

        Assert.Contains(sku, problema.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Los_dos_precios_deben_ser_mayores_que_cero()
    {
        using var operador = await api.OperadorAsync();

        var problema = await (await operador.PostAsJsonAsync("/api/productos", Producto(Ayudantes.Unico("SKU"), precio: 0, precioCompra: 0)))
            .ProblemaAsync(400, "validacion");

        Assert.True(problema.GetProperty("errors").TryGetProperty("precio", out _));
        Assert.True(problema.GetProperty("errors").TryGetProperty("precioCompra", out _));
    }

    [Fact]
    public async Task Crear_guarda_el_stock_minimo_y_empieza_con_stock_cero()
    {
        using var operador = await api.OperadorAsync();
        var respuesta = await operador.PostAsJsonAsync("/api/productos", Producto(Ayudantes.Unico("SKU"), stockMinimo: 5));
        var id = (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt32();

        var producto = await (await operador.GetAsync($"/api/productos/{id}")).LeerJsonAsync();

        Assert.Equal(5m, producto.GetProperty("stockMinimo").GetDecimal());
        Assert.Equal(0m, producto.GetProperty("stockActual").GetDecimal());
        Assert.Equal(7m, producto.GetProperty("precioCompra").GetDecimal());
    }

    [Fact]
    public async Task Editar_cambia_los_precios_y_el_minimo()
    {
        using var operador = await api.OperadorAsync();
        var sku = Ayudantes.Unico("SKU");
        var id = (await (await operador.PostAsJsonAsync("/api/productos", Producto(sku))).LeerJsonAsync()).GetProperty("id").GetInt32();

        var editado = await (await operador.PutAsJsonAsync($"/api/productos/{id}", Producto(sku, precio: 12m, precioCompra: 9m, stockMinimo: 3)))
            .LeerJsonAsync();

        Assert.Equal(12m, editado.GetProperty("precio").GetDecimal());
        Assert.Equal(9m, editado.GetProperty("precioCompra").GetDecimal());
        Assert.Equal(3m, editado.GetProperty("stockMinimo").GetDecimal());
    }

    [Fact]
    public async Task Editar_un_producto_que_no_existe_responde_404()
    {
        using var operador = await api.OperadorAsync();

        await (await operador.PutAsJsonAsync("/api/productos/999999", Producto(Ayudantes.Unico("SKU")))).ProblemaAsync(404, "no_encontrado");
    }

    [Fact]
    public async Task El_promotor_elige_productos_sin_ver_el_precio_de_compra()
    {
        using var operador = await api.OperadorAsync();
        var productoId = await operador.CrearProductoAsync(10m, stockInicial: 8);
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var cliente = api.ClienteCon(promotor.Token);

        var opciones = (await (await cliente.GetAsync("/api/productos/opciones")).LeerJsonAsync()).EnumerateArray().ToList();

        var opcion = Assert.Single(opciones, o => o.GetProperty("id").GetInt32() == productoId);
        Assert.Equal(8m, opcion.GetProperty("stockActual").GetDecimal());
        Assert.False(opcion.TryGetProperty("precioCompra", out _));
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/productos")).StatusCode);
    }
}
