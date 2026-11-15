using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

/// <summary>
/// Las categorías de los productos (7.3.29, tarea 29), que administra el administrador del sistema con los permisos de
/// productos.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class CategoriasTests(ApiDePrueba api)
{
    private static async Task<JsonElement> RegistrarAsync(HttpClient admin, string nombre)
    {
        var respuesta = await admin.PostAsJsonAsync("/api/categorias", new { nombre });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return await respuesta.LeerJsonAsync();
    }

    private static async Task<(int Productos, int Inactivos)> CantidadesAsync(HttpClient admin, int id)
    {
        var categoria = await (await admin.GetAsync($"/api/categorias/{id}")).LeerJsonAsync();
        return (categoria.GetProperty("productos").GetInt32(), categoria.GetProperty("inactivos").GetInt32());
    }

    [Fact]
    public async Task Una_categoria_se_registra_se_renombra_y_se_elimina()
    {
        using var admin = await api.AdminAsync();
        var nombre = Ayudantes.Unico("T29 Bebidas");

        var creada = await RegistrarAsync(admin, $"  {nombre} ");
        var id = creada.GetProperty("id").GetInt32();
        Assert.Equal(nombre, creada.GetProperty("nombre").GetString());
        Assert.Equal((0, 0), await CantidadesAsync(admin, id));

        var nuevo = Ayudantes.Unico("T29 Gaseosas");
        var renombrada = await (await admin.PutAsJsonAsync($"/api/categorias/{id}", new { nombre = nuevo })).LeerJsonAsync();
        Assert.Equal(nuevo, renombrada.GetProperty("nombre").GetString());
        // Cambiar solo sus mayúsculas no choca con ella misma.
        var enMinusculas = await (await admin.PutAsJsonAsync($"/api/categorias/{id}", new { nombre = nuevo.ToLowerInvariant() }))
            .LeerJsonAsync();
        Assert.Equal(nuevo.ToLowerInvariant(), enMinusculas.GetProperty("nombre").GetString());
        Assert.Contains((await (await admin.GetAsync("/api/categorias")).LeerJsonAsync()).EnumerateArray(),
            c => c.GetProperty("id").GetInt32() == id);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/categorias/{id}")).StatusCode);
        await (await admin.GetAsync($"/api/categorias/{id}")).ProblemaAsync(404, "no_encontrado");
    }

    [Fact]
    public async Task El_nombre_es_obligatorio_y_no_se_repite_sin_importar_mayusculas_ni_tildes()
    {
        using var admin = await api.AdminAsync();
        var nombre = Ayudantes.Unico("T29 Higiene");
        await RegistrarAsync(admin, nombre);
        var otra = (await RegistrarAsync(admin, Ayudantes.Unico("T29 Otra"))).GetProperty("id").GetInt32();

        var vacio = await (await admin.PostAsJsonAsync("/api/categorias", new { nombre = "   " })).ProblemaAsync(400, "validacion");
        Assert.Equal("Escribe el nombre de la categoría.", vacio.GetProperty("errors").GetProperty("nombre")[0].GetString());

        var enMayusculas = nombre.ToUpperInvariant();
        var repetida = await (await admin.PostAsJsonAsync("/api/categorias", new { nombre = enMayusculas }))
            .ProblemaAsync(409, "categoria_repetida");
        Assert.Equal($"Ya existe la categoría \"{enMayusculas}\".", repetida.GetProperty("detail").GetString());
        await (await admin.PostAsJsonAsync("/api/categorias", new { nombre = nombre.Replace("Higiene", "Higiéne") }))
            .ProblemaAsync(409, "categoria_repetida");
        await (await admin.PutAsJsonAsync($"/api/categorias/{otra}", new { nombre })).ProblemaAsync(409, "categoria_repetida");
    }

    [Fact]
    public async Task Una_categoria_con_productos_no_se_elimina_aunque_solo_tenga_inactivos()
    {
        using var admin = await api.AdminAsync();
        using var operador = await api.OperadorAsync();
        var nombre = Ayudantes.Unico("T29 Limpieza");
        var id = (await RegistrarAsync(admin, nombre)).GetProperty("id").GetInt32();
        var producto = await operador.CrearProductoAsync(10m, stockInicial: 0, categoriaId: id);

        Assert.Equal((1, 0), await CantidadesAsync(admin, id));
        var conActivos = await (await admin.DeleteAsync($"/api/categorias/{id}")).ProblemaAsync(409, "categoria_en_uso");
        Assert.Equal($"{nombre} tiene 1 producto: pásalo a otra categoría antes de eliminarla.",
            conActivos.GetProperty("detail").GetString());

        // Un producto inactivo conserva su categoría (7.3.29, ext. 5), y no se ve en Productos: el mensaje dice por qué.
        (await operador.DeleteAsync($"/api/productos/{producto}")).EnsureSuccessStatusCode();
        Assert.Equal((0, 1), await CantidadesAsync(admin, id));
        var conInactivos = await (await admin.DeleteAsync($"/api/categorias/{id}")).ProblemaAsync(409, "categoria_en_uso");
        Assert.Equal($"{nombre} tiene 1 producto inactivo, que conserva su categoría: no se puede eliminar.",
            conInactivos.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Cada_operacion_pide_su_permiso_de_productos_y_la_bitacora_dice_que_se_hizo()
    {
        using var admin = await api.AdminAsync();
        var id = (await RegistrarAsync(admin, Ayudantes.Unico("T29 Permisos"))).GetProperty("id").GetInt32();
        var (_, rol) = await admin.CrearRolAsync(Permisos.Parametros.RegistrarProducto);
        var soloRegistra = await api.CrearUsuarioAsync(rol);
        using var registra = api.ClienteCon(soloRegistra.Token);
        using var promotor = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Promotor)).Token);
        var cuerpo = new { nombre = Ayudantes.Unico("T29 Nueva") };

        await (await promotor.GetAsync("/api/categorias")).ProblemaAsync(403, "sin_permiso");
        await (await promotor.PostAsJsonAsync("/api/categorias", cuerpo)).ProblemaAsync(403, "sin_permiso");
        await (await promotor.PutAsJsonAsync($"/api/categorias/{id}", cuerpo)).ProblemaAsync(403, "sin_permiso");
        await (await promotor.DeleteAsync($"/api/categorias/{id}")).ProblemaAsync(403, "sin_permiso");

        Assert.Equal(HttpStatusCode.Created, (await registra.PostAsJsonAsync("/api/categorias", cuerpo)).StatusCode);
        await (await registra.PutAsJsonAsync($"/api/categorias/{id}", new { nombre = Ayudantes.Unico("T29") })).ProblemaAsync(403, "sin_permiso");
        await (await registra.DeleteAsync($"/api/categorias/{id}")).ProblemaAsync(403, "sin_permiso");

        // El permiso es "Registrar productos", pero la bitácora dice qué se hizo.
        var registros = (await (await admin.GetAsync($"/api/bitacora?usuarioId={soloRegistra.Id}&tamano=10")).LeerJsonAsync())
            .GetProperty("items");
        Assert.Equal(("Parámetros y Configuraciones", "Registrar categorías (POST /api/categorias)"),
            (registros[0].GetProperty("modulo").GetString(), registros[0].GetProperty("accion").GetString()));
    }

    [Fact]
    public async Task Las_opciones_las_piden_tambien_quienes_abren_Inventario()
    {
        using var admin = await api.AdminAsync();
        var nombre = Ayudantes.Unico("T29 Opción");
        var id = (await RegistrarAsync(admin, nombre)).GetProperty("id").GetInt32();
        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);
        using var gerente = api.ClienteCon((await api.CrearUsuarioAsync(Roles.GerenteGeneral)).Token);

        var opciones = (await (await secretaria.GetAsync("/api/categorias/opciones")).LeerJsonAsync()).EnumerateArray().ToList();
        var opcion = Assert.Single(opciones, c => c.GetProperty("id").GetInt32() == id);
        Assert.Equal(nombre, opcion.GetProperty("nombre").GetString());
        Assert.False(opcion.TryGetProperty("productos", out _));
        await (await secretaria.GetAsync("/api/categorias")).ProblemaAsync(403, "sin_permiso");
        // El gerente filtra el reporte de inventario con las categorías que trae el reporte.
        await (await gerente.GetAsync("/api/categorias/opciones")).ProblemaAsync(403, "sin_permiso");
    }
}
