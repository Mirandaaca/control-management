using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class ProductosTests(ApiDePrueba api)
{
    private static object Producto(string sku, decimal precio = 10m, decimal precioCompra = 7m, decimal stockMinimo = 0m,
        int? categoriaId = null) =>
        new { sku, descripcion = $"Producto {sku}", categoriaId = categoriaId ?? ApiDePrueba.CategoriaDePruebasId, precio, precioCompra, stockMinimo };

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
        // El catálogo de la app filtra por categoría (7.3.7, paso 2) y la lee como texto: así la sigue leyendo el APK
        // instalado, aunque la categoría ya sea una entidad (tarea 29).
        Assert.Equal(JsonValueKind.String, opcion.GetProperty("categoria").ValueKind);
        Assert.Equal(ApiDePrueba.NombreDeLaCategoriaDePruebas, opcion.GetProperty("categoria").GetString());
        Assert.False(opcion.TryGetProperty("precioCompra", out _));
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/productos")).StatusCode);
    }

    [Fact]
    public async Task Un_producto_pide_una_categoria_que_exista()
    {
        using var operador = await api.OperadorAsync();

        var sinCategoria = await (await operador.PostAsJsonAsync("/api/productos", Producto(Ayudantes.Unico("SKU"), categoriaId: 0)))
            .ProblemaAsync(400, "validacion");
        Assert.Equal("Elige la categoría del producto.", sinCategoria.GetProperty("errors").GetProperty("categoriaId")[0].GetString());

        var noExiste = await (await operador.PostAsJsonAsync("/api/productos", Producto(Ayudantes.Unico("SKU"), categoriaId: 999999)))
            .ProblemaAsync(400, "categoria_no_existe");
        Assert.True(noExiste.GetProperty("errors").TryGetProperty("categoriaId", out _));

        var id = await operador.CrearProductoAsync(10m, stockInicial: 0);
        await (await operador.PutAsJsonAsync($"/api/productos/{id}", Producto(Ayudantes.Unico("SKU"), categoriaId: 999999)))
            .ProblemaAsync(400, "categoria_no_existe");
    }

    [Fact]
    public async Task Trae_su_categoria_la_cambia_y_se_filtra_y_se_busca_por_ella()
    {
        using var operador = await api.OperadorAsync();
        var nombre = Ayudantes.Unico("Bebidas");
        var bebidas = await operador.CrearCategoriaAsync(nombre);
        var deBebidas = await operador.CrearProductoAsync(10m, stockInicial: 0, categoriaId: bebidas);
        // Otro, en la categoría de la corrida: el filtro no lo trae.
        await operador.CrearProductoAsync(10m, stockInicial: 0);

        var producto = await (await operador.GetAsync($"/api/productos/{deBebidas}")).LeerJsonAsync();
        Assert.Equal((bebidas, nombre), (producto.GetProperty("categoria").GetProperty("id").GetInt32(),
            producto.GetProperty("categoria").GetProperty("nombre").GetString()));

        // El filtro (decisión 6) y el buscador, que compara también el nombre de la categoría.
        Assert.Equal([deBebidas], await IdsAsync(operador, $"/api/productos?categoriaId={bebidas}"));
        Assert.Equal([deBebidas], await IdsAsync(operador, $"/api/productos?buscar={nombre}"));
        var inventario = (await (await operador.GetAsync($"/api/inventario?categoriaId={bebidas}")).LeerJsonAsync())
            .GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(deBebidas, Assert.Single(inventario).GetProperty("productoId").GetInt32());
        Assert.Equal(nombre, inventario[0].GetProperty("categoria").GetString());

        var sku = producto.GetProperty("sku").GetString()!;
        var cambiado = await (await operador.PutAsJsonAsync($"/api/productos/{deBebidas}", Producto(sku, categoriaId: ApiDePrueba.CategoriaDePruebasId)))
            .LeerJsonAsync();
        Assert.Equal(ApiDePrueba.CategoriaDePruebasId, cambiado.GetProperty("categoria").GetProperty("id").GetInt32());
        Assert.Empty(await IdsAsync(operador, $"/api/productos?categoriaId={bebidas}"));
        Assert.Equal([deBebidas], await IdsAsync(operador, $"/api/productos?categoriaId={ApiDePrueba.CategoriaDePruebasId}&buscar={sku}"));
    }

    [Fact]
    public async Task Un_producto_inactivo_no_se_vuelve_a_desactivar()
    {
        using var operador = await api.OperadorAsync();
        var id = await operador.CrearProductoAsync(10m, stockInicial: 0);
        Assert.Equal(HttpStatusCode.NoContent, (await operador.DeleteAsync($"/api/productos/{id}")).StatusCode);

        // 7.3.29, paso 4.1: antes respondía 204 otra vez, sin hacer nada.
        var problema = await (await operador.DeleteAsync($"/api/productos/{id}")).ProblemaAsync(400, "producto_inactivo");
        Assert.Equal($"Producto {(await (await operador.GetAsync($"/api/productos/{id}")).LeerJsonAsync()).GetProperty("sku").GetString()} ya está inactivo.",
            problema.GetProperty("detail").GetString());
    }

    private static async Task<List<int>> IdsAsync(HttpClient cliente, string ruta) =>
        (await (await cliente.GetAsync(ruta)).LeerJsonAsync()).GetProperty("items").EnumerateArray()
            .Select(p => p.GetProperty("id").GetInt32()).ToList();
}
