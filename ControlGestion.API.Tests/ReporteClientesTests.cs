using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using ControlGestion.API.Data;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>
/// Clientes nuevos y recurrentes (6.3, 7.3.24). Los clientes y sus HP se mueven a fechas de 2003, que ninguna otra prueba
/// usa: así el período solo trae los de cada prueba (cada corrida crea su base).
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class ReporteClientesTests(ApiDePrueba api)
{
    private static DateTime Dia(int mes, int dia) => new(2003, mes, dia, 16, 0, 0, DateTimeKind.Utc);

    private async Task RegistradoElAsync(int clienteId, DateTime fecha)
    {
        using var scope = api.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Clientes.Where(c => c.Id == clienteId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.FechaCreacion, fecha));
    }

    private async Task<int> CompraElAsync(HttpClient operador, int clienteId, int productoId, DateTime fecha, bool anular = false)
    {
        var hojaId = await operador.CrearHojaPedidoAsync(clienteId, productoId, 1, credito: false);
        (await operador.PostAsync($"/api/hojas-pedido/{hojaId}/confirmar", null)).EnsureSuccessStatusCode();
        if (anular)
            (await operador.PostAsJsonAsync($"/api/hojas-pedido/{hojaId}/anular", new { motivo = "Venta duplicada" })).EnsureSuccessStatusCode();
        using var scope = api.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().HojasPedido.Where(h => h.Id == hojaId)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.FechaRegistro, fecha));
        return hojaId;
    }

    private sealed record Escenario(int Nuevo, int NuevoQueCompro, int Recurrente);

    /// <summary>
    /// En ese mes de 2003: dos nuevos (uno compró), un recurrente con dos compras y dos viejos que no cuentan. Cada prueba usa
    /// su mes: los datos de una quedan en la base para las demás.
    /// </summary>
    private async Task<Escenario> MesAsync(HttpClient operador, int mes)
    {
        var zonaId = await operador.CrearZonaAsync();
        var productoId = await operador.CrearProductoAsync(1m, stockInicial: 20);
        async Task<int> ClienteAsync(DateTime registro)
        {
            var id = await operador.CrearClienteAsync(zonaId);
            await RegistradoElAsync(id, registro);
            return id;
        }

        var nuevo = await ClienteAsync(Dia(mes, 10));
        var nuevoQueCompro = await ClienteAsync(Dia(mes, 11));
        await CompraElAsync(operador, nuevoQueCompro, productoId, Dia(mes, 11));
        var recurrente = await ClienteAsync(Dia(1, 2));
        await CompraElAsync(operador, recurrente, productoId, Dia(mes, 12));
        await CompraElAsync(operador, recurrente, productoId, Dia(mes, 20));
        await CompraElAsync(operador, recurrente, productoId, Dia(mes + 1, 2));
        // Registrado antes, sin compras en el mes; y otro cuya única compra del mes se anuló.
        await CompraElAsync(operador, await ClienteAsync(Dia(1, 3)), productoId, Dia(mes - 1, 28));
        await CompraElAsync(operador, await ClienteAsync(Dia(1, 4)), productoId, Dia(mes, 15), anular: true);
        return new Escenario(nuevo, nuevoQueCompro, recurrente);
    }

    private const string Mayo = "desde=2003-05-01&hasta=2003-05-31";

    private async Task<HttpClient> GerenteAsync() => api.ClienteCon((await api.CrearUsuarioAsync(Roles.GerenteGeneral)).Token);

    [Fact]
    public async Task Nuevos_son_los_registrados_en_el_periodo_y_recurrentes_los_de_antes_que_compraron_en_el()
    {
        using var operador = await api.OperadorAsync();
        var escenario = await MesAsync(operador, 5);
        using var gerente = await GerenteAsync();

        var reporte = await (await gerente.GetAsync($"/api/reportes/clientes?{Mayo}")).LeerJsonAsync();

        Assert.Equal(2, reporte.GetProperty("cantidadNuevos").GetInt32());
        Assert.Equal(1, reporte.GetProperty("cantidadRecurrentes").GetInt32());
        // Del más reciente al más antiguo: el que compró sale solo como nuevo.
        Assert.Equal([escenario.NuevoQueCompro, escenario.Nuevo], reporte.GetProperty("nuevos").GetProperty("items").EnumerateArray()
            .Select(c => c.GetProperty("id").GetInt32()));
        var recurrente = reporte.GetProperty("recurrentes").GetProperty("items")[0];
        Assert.Equal(escenario.Recurrente, recurrente.GetProperty("id").GetInt32());
        Assert.Equal(2, recurrente.GetProperty("compras").GetInt32());
        Assert.False(string.IsNullOrEmpty(recurrente.GetProperty("zona").GetProperty("nombre").GetString()));

        var pagina = await (await gerente.GetAsync($"/api/reportes/clientes?{Mayo}&tamano=1&paginaNuevos=2")).LeerJsonAsync();
        Assert.Equal(escenario.Nuevo, pagina.GetProperty("nuevos").GetProperty("items")[0].GetProperty("id").GetInt32());
        Assert.Equal(2, pagina.GetProperty("nuevos").GetProperty("totalPaginas").GetInt32());
    }

    [Fact]
    public async Task El_Excel_trae_las_dos_listas_y_sus_cantidades()
    {
        using var operador = await api.OperadorAsync();
        await MesAsync(operador, 8);
        using var gerente = await GerenteAsync();

        var respuesta = await gerente.GetAsync("/api/reportes/clientes/excel?desde=2003-08-01&hasta=2003-08-31");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var libro = new XLWorkbook(await respuesta.Content.ReadAsStreamAsync());
        Assert.Equal(3, libro.Worksheet("Nuevos").LastRowUsed()!.RowNumber());
        Assert.Equal(new DateTime(2003, 8, 11, 12, 0, 0), libro.Worksheet("Nuevos").Cell(2, 5).GetDateTime());
        Assert.Equal(2, libro.Worksheet("Recurrentes").Cell(2, 5).GetValue<int>());
        var totales = libro.Worksheet("Totales");
        Assert.Equal("01/08/2003 al 31/08/2003", totales.Cell(2, 1).GetString());
        Assert.Equal((2, 1), (totales.Cell(2, 2).GetValue<int>(), totales.Cell(2, 3).GetValue<int>()));
    }

    [Fact]
    public async Task Sin_cartera_completa_solo_cuenta_los_clientes_de_su_cartera()
    {
        using var operador = await api.OperadorAsync();
        using var admin = await api.AdminAsync();
        var (_, rol) = await admin.CrearRolAsync(Permisos.Reportes.ClientesNuevosRecurrentes, Permisos.Territorial.RecorrerRuta);
        var usuario = await api.CrearUsuarioAsync(rol);
        var suyo = await operador.CrearClienteAsync(await operador.CrearZonaAsync(usuario.Id));
        var ajeno = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        await RegistradoElAsync(suyo, Dia(7, 5));
        await RegistradoElAsync(ajeno, Dia(7, 6));
        using var cliente = api.ClienteCon(usuario.Token);

        var reporte = await (await cliente.GetAsync("/api/reportes/clientes?desde=2003-07-01&hasta=2003-07-31")).LeerJsonAsync();

        Assert.Equal([suyo], reporte.GetProperty("nuevos").GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task Pide_las_dos_fechas_sin_clientes_no_exporta_y_pide_su_permiso()
    {
        using var gerente = await GerenteAsync();

        var sinFechas = await (await gerente.GetAsync("/api/reportes/clientes")).ProblemaAsync(400, "validacion");
        Assert.Equal("Elige el primer día del período.", sinFechas.GetProperty("errors").GetProperty("desde")[0].GetString());
        Assert.Equal("Elige el último día del período.", sinFechas.GetProperty("errors").GetProperty("hasta")[0].GetString());
        await (await gerente.GetAsync("/api/reportes/clientes?desde=2003-05-02&hasta=2003-05-01")).ProblemaAsync(400, "validacion");
        await (await gerente.GetAsync("/api/reportes/clientes/excel?desde=2003-12-01&hasta=2003-12-31")).ProblemaAsync(400, "reporte_sin_datos");

        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await secretaria.GetAsync($"/api/reportes/clientes?{Mayo}")).StatusCode);
    }
}
