using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Data;
using ControlGestion.API.Models;
using ControlGestion.API.Tests.Infraestructura;
using ControlGestion.API.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class CobranzasTests(ApiDePrueba api)
{
    private record Escenario(HttpClient Operador, int ZonaId, int ProductoId) : IDisposable
    {
        public void Dispose() => Operador.Dispose();
    }

    [Fact]
    public async Task Una_HP_tiene_a_lo_sumo_una_cobranza()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var cobranzaId = await operador.CrearCobranzaAsync(clienteId, await operador.CrearProductoAsync(10m), 1);

        // La API nunca crea una segunda: el índice único lo garantiza también en la base (7.3.5, paso 3.4).
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var primera = await db.Cobranzas.AsNoTracking().SingleAsync(c => c.Id == cobranzaId);
        db.Cobranzas.Add(new Cobranza
        {
            Numero = Ayudantes.Unico("COB"), ClienteId = primera.ClienteId, HojaPedidoId = primera.HojaPedidoId,
            PromotorId = primera.PromotorId, MontoTotal = 1m, FechaEmision = DateTime.UtcNow, FechaVencimiento = DateTime.UtcNow
        });

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(MySqlErrorCode.DuplicateKeyEntry, Assert.IsType<MySqlException>(error.InnerException).ErrorCode);
    }

    private async Task<Escenario> PrepararAsync(decimal precio = 10m)
    {
        var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        return new Escenario(operador, zonaId, await operador.CrearProductoAsync(precio));
    }

    private static async Task<List<int>> IdsDeCobranzasAsync(HttpClient cliente, string consulta = "") =>
        (await (await cliente.GetAsync($"/api/cobranzas{consulta}")).LeerJsonAsync())
            .GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).ToList();

    [Fact]
    public async Task Un_promotor_solo_ve_las_cobranzas_de_sus_clientes()
    {
        using var escenario = await PrepararAsync();
        var (operador, zonaId, productoId) = escenario;
        var promotorA = await api.CrearUsuarioAsync(Roles.Promotor);
        var promotorB = await api.CrearUsuarioAsync(Roles.Promotor);
        var deA = await operador.CrearCobranzaAsync(await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotorA.Id)), productoId, 1);
        var deB = await operador.CrearCobranzaAsync(await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotorB.Id)), productoId, 1);
        using var clienteA = api.ClienteCon(promotorA.Token);

        var visibles = await IdsDeCobranzasAsync(clienteA);

        Assert.Contains(deA, visibles);
        Assert.DoesNotContain(deB, visibles);
        Assert.Contains(deB, await IdsDeCobranzasAsync(operador));
    }

    [Fact]
    public async Task Un_promotor_no_ve_ni_paga_la_cobranza_de_un_cliente_ajeno()
    {
        using var escenario = await PrepararAsync();
        var (operador, zonaId, productoId) = escenario;
        var promotorA = await api.CrearUsuarioAsync(Roles.Promotor);
        var promotorB = await api.CrearUsuarioAsync(Roles.Promotor);
        var deB = await operador.CrearCobranzaAsync(await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotorB.Id)), productoId, 1);
        using var clienteA = api.ClienteCon(promotorA.Token);

        await (await clienteA.GetAsync($"/api/cobranzas/{deB}")).ProblemaAsync(404, "no_encontrado");
        await (await clienteA.PostAsJsonAsync($"/api/cobranzas/{deB}/pagos", new { monto = 1, metodoPago = 0 }))
            .ProblemaAsync(404, "no_encontrado");
        var cobranza = await (await operador.GetAsync($"/api/cobranzas/{deB}")).LeerJsonAsync();
        Assert.Equal(0, cobranza.GetProperty("pagos").GetArrayLength());
    }

    [Fact]
    public async Task Un_promotor_cobra_las_cobranzas_de_sus_clientes()
    {
        using var escenario = await PrepararAsync();
        var (operador, zonaId, productoId) = escenario;
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var cobranzaId = await operador.CrearCobranzaAsync(await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id)), productoId, 1);
        using var cliente = api.ClienteCon(promotor.Token);

        var respuesta = await cliente.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos", new { monto = 4, metodoPago = 0 });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(4m, (await respuesta.LeerJsonAsync()).GetProperty("montoPagado").GetDecimal());
    }

    [Fact]
    public async Task La_cobranza_queda_a_nombre_del_promotor_que_vendio()
    {
        using var escenario = await PrepararAsync();
        var (operador, _, productoId) = escenario;
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id));
        using var vendedor = api.ClienteCon(promotor.Token);

        await vendedor.CrearCobranzaAsync(clienteId, productoId, 1);

        var cobranza = (await (await operador.GetAsync($"/api/cobranzas?clienteId={clienteId}")).LeerJsonAsync())
            .GetProperty("items").EnumerateArray().Single();
        Assert.Equal(promotor.Id, cobranza.GetProperty("promotor").GetProperty("id").GetString());
    }

    [Fact]
    public async Task Un_pago_con_QR_pide_su_referencia()
    {
        using var escenario = await PrepararAsync();
        var (operador, zonaId, productoId) = escenario;
        var cobranzaId = await operador.CrearCobranzaAsync(await operador.CrearClienteAsync(zonaId), productoId, 1);

        var sinReferencia = await (await operador.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos",
            new { monto = 4, metodoPago = 1, referencia = "  " })).ProblemaAsync(400, "validacion");
        Assert.True(sinReferencia.GetProperty("errors").TryGetProperty("referencia", out _));

        (await operador.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos",
            new { monto = 4, metodoPago = 1, referencia = "QR-778812" })).EnsureSuccessStatusCode();
        var pago = (await (await operador.GetAsync($"/api/cobranzas/{cobranzaId}")).LeerJsonAsync()).GetProperty("pagos")[0];
        Assert.Equal(1, pago.GetProperty("metodoPago").GetInt32());
        Assert.Equal("QR-778812", pago.GetProperty("referencia").GetString());
    }

    [Theory]
    [InlineData(5, 4)]
    [InlineData(0, 0)]
    public async Task Un_metodo_que_no_existe_o_un_monto_en_cero_responden_400(int metodoPago, int monto)
    {
        using var escenario = await PrepararAsync();
        var (operador, zonaId, productoId) = escenario;
        var cobranzaId = await operador.CrearCobranzaAsync(await operador.CrearClienteAsync(zonaId), productoId, 1);

        var problema = await (await operador.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos", new { monto, metodoPago }))
            .ProblemaAsync(400, "validacion");

        Assert.True(problema.GetProperty("errors").TryGetProperty(metodoPago == 5 ? "metodoPago" : "monto", out _));
        var cobranza = await (await operador.GetAsync($"/api/cobranzas/{cobranzaId}")).LeerJsonAsync();
        Assert.Equal(0, cobranza.GetProperty("pagos").GetArrayLength());
    }

    [Fact]
    public async Task El_gerente_general_marca_una_deuda_como_incobrable()
    {
        using var escenario = await PrepararAsync();
        var (operador, zonaId, productoId) = escenario;
        var cobranzaId = await operador.CrearCobranzaAsync(await operador.CrearClienteAsync(zonaId), productoId, 1);
        var gerente = await api.CrearUsuarioAsync(Roles.GerenteGeneral);
        using var cliente = api.ClienteCon(gerente.Token);

        var marcada = await (await cliente.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/incobrable",
            new { observacion = "El negocio cerró" })).LeerJsonAsync();

        Assert.Equal(4, marcada.GetProperty("estado").GetInt32());
    }

    [Fact]
    public async Task El_resumen_suma_solo_lo_que_se_debe_en_las_cobranzas_abiertas()
    {
        using var escenario = await PrepararAsync(precio: 10m);
        var (operador, zonaId, productoId) = escenario;
        var clienteId = await operador.CrearClienteAsync(zonaId);
        var parcial = await operador.CrearCobranzaAsync(clienteId, productoId, 3);
        await operador.CrearCobranzaAsync(clienteId, productoId, 2);
        var incobrable = await operador.CrearCobranzaAsync(clienteId, productoId, 5);
        (await operador.PostAsJsonAsync($"/api/cobranzas/{parcial}/pagos", new { monto = 12, metodoPago = 0 })).EnsureSuccessStatusCode();
        (await operador.PostAsJsonAsync($"/api/cobranzas/{incobrable}/incobrable", new { observacion = "Prueba" })).EnsureSuccessStatusCode();

        var resumen = await (await operador.GetAsync($"/api/cobranzas/resumen?clienteId={clienteId}")).LeerJsonAsync();

        // 30 − 12 de la parcial y 20 de la pendiente; la incobrable ya no cuenta.
        Assert.Equal(38m, resumen.GetProperty("porCobrar").GetDecimal());
        Assert.Equal(2, resumen.GetProperty("pendientes").GetInt32());
        Assert.Equal(0, resumen.GetProperty("vencidas").GetInt32());
    }

    [Fact]
    public async Task La_lista_viene_paginada_y_se_busca_por_numero()
    {
        using var escenario = await PrepararAsync();
        var (operador, zonaId, productoId) = escenario;
        var clienteId = await operador.CrearClienteAsync(zonaId);
        var primera = await operador.CrearCobranzaAsync(clienteId, productoId, 1);
        await operador.CrearCobranzaAsync(clienteId, productoId, 1);
        var numero = (await (await operador.GetAsync($"/api/cobranzas/{primera}")).LeerJsonAsync()).GetProperty("numero").GetString();

        var pagina = await (await operador.GetAsync($"/api/cobranzas?clienteId={clienteId}&tamano=1")).LeerJsonAsync();

        Assert.Equal(2, pagina.GetProperty("total").GetInt32());
        Assert.Equal(1, pagina.GetProperty("items").GetArrayLength());
        Assert.Equal([primera], await IdsDeCobranzasAsync(operador, $"?buscar={numero}"));
    }

    [Fact]
    public async Task El_filtro_por_dia_usa_el_dia_de_Bolivia()
    {
        using var escenario = await PrepararAsync();
        var (operador, zonaId, productoId) = escenario;
        var clienteId = await operador.CrearClienteAsync(zonaId);
        var cobranzaId = await operador.CrearCobranzaAsync(clienteId, productoId, 1);
        var hoyEnBolivia = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-4));

        var deHoy = await IdsDeCobranzasAsync(operador, $"?clienteId={clienteId}&desde={hoyEnBolivia:yyyy-MM-dd}&hasta={hoyEnBolivia:yyyy-MM-dd}");
        var deManana = await IdsDeCobranzasAsync(operador, $"?clienteId={clienteId}&desde={hoyEnBolivia.AddDays(1):yyyy-MM-dd}");

        Assert.Equal([cobranzaId], deHoy);
        Assert.Empty(deManana);
    }

    [Fact]
    public async Task Marcar_incobrable_exige_una_observacion()
    {
        using var escenario = await PrepararAsync();
        var (operador, zonaId, productoId) = escenario;
        var cobranzaId = await operador.CrearCobranzaAsync(await operador.CrearClienteAsync(zonaId), productoId, 1);

        var problema = await (await operador.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/incobrable", new { observacion = "" }))
            .ProblemaAsync(400, "validacion");
        Assert.True(problema.GetProperty("errors").TryGetProperty("observacion", out _));

        var marcada = await (await operador.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/incobrable",
            new { observacion = "El cliente cerró su negocio" })).LeerJsonAsync();
        Assert.Equal(4, marcada.GetProperty("estado").GetInt32());
        Assert.Equal("El cliente cerró su negocio", marcada.GetProperty("observaciones").GetString());
    }

    [Fact]
    public async Task Una_deuda_ya_incobrable_no_se_vuelve_a_marcar_y_conserva_su_motivo()
    {
        using var escenario = await PrepararAsync();
        var (operador, zonaId, productoId) = escenario;
        var cobranzaId = await operador.CrearCobranzaAsync(await operador.CrearClienteAsync(zonaId), productoId, 1);
        (await operador.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/incobrable", new { observacion = "El cliente se mudó" }))
            .EnsureSuccessStatusCode();

        await (await operador.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/incobrable", new { observacion = "Otro motivo" }))
            .ProblemaAsync(400, "cobranza_ya_incobrable");

        var cobranza = await (await operador.GetAsync($"/api/cobranzas/{cobranzaId}")).LeerJsonAsync();
        Assert.Equal("El cliente se mudó", cobranza.GetProperty("observaciones").GetString());
    }
}
