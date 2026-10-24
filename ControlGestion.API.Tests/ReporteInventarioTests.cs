using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using ControlGestion.API.Data;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>El reporte de inventario (6.4, 7.3.25), del gerente general y la secretaria.</summary>
[Collection(ColeccionApi.Nombre)]
public class ReporteInventarioTests(ApiDePrueba api)
{
    private sealed record Escenario(string Categoria, int Sobrado, int Justo, int Falta, int Inactivo);

    /// <summary>
    /// Una categoría propia con tres productos: uno sobre su mínimo, uno justo en él y uno debajo; y uno inactivo.
    /// </summary>
    private static async Task<Escenario> CategoriaAsync(HttpClient operador)
    {
        var categoria = Ayudantes.Unico("Cat");
        async Task<int> ProductoAsync(string nombre, decimal stock, decimal minimo)
        {
            var sku = Ayudantes.Unico("P");
            var respuesta = await operador.PostAsJsonAsync("/api/productos",
                new { sku, descripcion = $"{nombre} {sku}", precio = 10m, precioCompra = 8m, categoria, stockMinimo = minimo });
            respuesta.EnsureSuccessStatusCode();
            var id = (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt32();
            (await operador.RegistrarIngresoAsync(id, stock)).EnsureSuccessStatusCode();
            return id;
        }

        var sobrado = await ProductoAsync("A sobrado", 50, 10);
        var justo = await ProductoAsync("B justo", 10, 10);
        var falta = await ProductoAsync("C falta", 3, 10);
        var inactivo = await ProductoAsync("D inactivo", 1, 10);
        (await operador.DeleteAsync($"/api/productos/{inactivo}")).EnsureSuccessStatusCode();
        return new Escenario(categoria, sobrado, justo, falta, inactivo);
    }

    private static List<int> Ids(JsonElement lista) => lista.EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).ToList();

    [Fact]
    public async Task Trae_el_stock_de_los_activos_de_la_categoria_y_aparte_los_que_estan_en_su_minimo_o_debajo()
    {
        using var operador = await api.OperadorAsync();
        var escenario = await CategoriaAsync(operador);
        using var gerente = api.ClienteCon((await api.CrearUsuarioAsync(Roles.GerenteGeneral)).Token);

        var reporte = await (await gerente.GetAsync($"/api/reportes/inventario?categoria={escenario.Categoria}")).LeerJsonAsync();

        Assert.Contains(escenario.Categoria, reporte.GetProperty("categorias").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal([escenario.Sobrado, escenario.Justo, escenario.Falta], Ids(reporte.GetProperty("productos")));
        var bajo = reporte.GetProperty("bajoElMinimo");
        Assert.Equal([escenario.Justo, escenario.Falta], Ids(bajo));
        Assert.Equal([0m, 7m], bajo.EnumerateArray().Select(p => p.GetProperty("falta").GetDecimal()));
        var producto = reporte.GetProperty("productos")[0];
        Assert.StartsWith("P-", producto.GetProperty("codigo").GetString());
        Assert.Equal((50m, 10m), (producto.GetProperty("stockActual").GetDecimal(), producto.GetProperty("stockMinimo").GetDecimal()));
    }

    [Fact]
    public async Task Los_movimientos_son_los_de_la_categoria_y_del_periodo_del_mas_reciente_y_paginados()
    {
        using var operador = await api.OperadorAsync();
        var escenario = await CategoriaAsync(operador);
        using (var scope = api.Services.CreateScope())
        {
            // El ingreso del producto justo, el 02/02/2004 a las 21:30 de Bolivia (en UTC ya es el 3).
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fecha = new DateTime(2004, 2, 3, 1, 30, 0, DateTimeKind.Utc);
            await db.MovimientosInventario.Where(m => m.ProductoId == escenario.Justo)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Fecha, fecha));
        }
        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);

        var todos = await (await secretaria.GetAsync($"/api/reportes/inventario?categoria={escenario.Categoria}&tamano=2"))
            .LeerJsonAsync();
        var movimientos = todos.GetProperty("movimientos");
        Assert.Equal(4, movimientos.GetProperty("total").GetInt32());
        Assert.Equal(2, movimientos.GetProperty("totalPaginas").GetInt32());
        var masReciente = movimientos.GetProperty("items")[0];
        Assert.Equal(0, masReciente.GetProperty("tipo").GetInt32());
        Assert.False(string.IsNullOrEmpty(masReciente.GetProperty("usuario").GetProperty("nombreCompleto").GetString()));

        var delDia = await (await secretaria.GetAsync($"/api/reportes/inventario?categoria={escenario.Categoria}&desde=2004-02-02&hasta=2004-02-02"))
            .LeerJsonAsync();
        Assert.Single(delDia.GetProperty("movimientos").GetProperty("items").EnumerateArray());
        Assert.Equal(10m, delDia.GetProperty("movimientos").GetProperty("items")[0].GetProperty("stockResultante").GetDecimal());
    }

    [Fact]
    public async Task El_Excel_trae_el_stock_los_que_estan_bajo_el_minimo_y_los_movimientos()
    {
        using var operador = await api.OperadorAsync();
        var escenario = await CategoriaAsync(operador);
        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);

        var respuesta = await secretaria.GetAsync($"/api/reportes/inventario/excel?categoria={escenario.Categoria}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var libro = new XLWorkbook(await respuesta.Content.ReadAsStreamAsync());
        Assert.Equal(4, libro.Worksheet("Stock").LastRowUsed()!.RowNumber());
        var bajo = libro.Worksheet("Bajo el mínimo");
        Assert.Equal("Falta", bajo.Cell(1, 6).GetString());
        Assert.Equal(7m, bajo.Cell(3, 6).GetValue<decimal>());
        var movimientos = libro.Worksheet("Movimientos");
        Assert.Equal(5, movimientos.LastRowUsed()!.RowNumber());
        Assert.Equal("Entrada", movimientos.Cell(2, 3).GetString());

        var sinMovimientos = await secretaria.GetAsync($"/api/reportes/inventario/excel?categoria={escenario.Categoria}&desde=2001-01-01&hasta=2001-01-01");
        using var vacio = new XLWorkbook(await sinMovimientos.Content.ReadAsStreamAsync());
        Assert.Equal("No hay movimientos en el período.", vacio.Worksheet("Movimientos").Cell(2, 1).GetString());
    }

    [Fact]
    public async Task Rechaza_un_rango_al_reves_y_pide_su_permiso()
    {
        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);
        await (await secretaria.GetAsync("/api/reportes/inventario?desde=2026-10-21&hasta=2026-10-20")).ProblemaAsync(400, "validacion");

        using var promotor = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Promotor)).Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await promotor.GetAsync("/api/reportes/inventario")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await promotor.GetAsync("/api/reportes/inventario/excel")).StatusCode);
    }
}
