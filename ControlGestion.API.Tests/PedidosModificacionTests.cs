using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class PedidosModificacionTests(ApiDePrueba api)
{
    private static async Task<JsonElement> PedidoDe(HttpClient cliente, int pedidoId) =>
        await (await cliente.GetAsync($"/api/pedidos/{pedidoId}")).LeerJsonAsync();

    private static int EstadoDe(JsonElement pedido) => pedido.GetProperty("estado").GetProperty("id").GetInt32();

    private static string EnBolivia(DateOnly dia, int hora, int minuto = 0) => $"{dia:yyyy-MM-dd}T{hora:00}:{minuto:00}:00";

    private static DateOnly Manana => HoraBolivia.Hoy(TimeProvider.System).AddDays(1);

    private static Task<HttpResponseMessage> ModificarAsync(HttpClient cliente, int pedidoId, int productoId,
        decimal cantidad = 1, string? fecha = null, string direccion = "Otra dirección", decimal descuento = 0) =>
        cliente.PutAsJsonAsync($"/api/pedidos/{pedidoId}", new
        {
            fechaEntregaEstimada = fecha ?? Ayudantes.MananaEnBolivia(),
            direccionEntrega = direccion,
            descuento,
            observaciones = "Llamar antes de llegar",
            detalles = new[] { new { productoId, cantidad, descuento = 0 } }
        });

    private static Task<HttpResponseMessage> ReprogramarAsync(HttpClient cliente, int pedidoId, string? fecha,
        string motivo = "El cliente no estará") =>
        cliente.PostAsJsonAsync($"/api/pedidos/{pedidoId}/reprogramar", new { fechaEntregaEstimada = fecha, motivo });

    private static async Task PasarAEnRutaAsync(HttpClient cliente, int pedidoId) =>
        (await cliente.PostAsJsonAsync($"/api/pedidos/{pedidoId}/avanzar", new { })).EnsureSuccessStatusCode();

    private static async Task<(int PedidoId, int ClienteId)> PedidoNuevoAsync(HttpClient operador, int? zonaId = null)
    {
        var clienteId = await operador.CrearClienteAsync(zonaId ?? await operador.CrearZonaAsync());
        return (await operador.CrearPedidoAsync(clienteId, await operador.CrearProductoAsync(10m), 1), clienteId);
    }

    [Fact]
    public async Task Modificar_cambia_productos_fecha_direccion_y_recalcula_el_total()
    {
        using var operador = await api.OperadorAsync();
        var (pedidoId, clienteId) = await PedidoNuevoAsync(operador);
        var otroProducto = await operador.CrearProductoAsync(7.5m);

        var pedido = await (await ModificarAsync(operador, pedidoId, otroProducto, cantidad: 4, fecha: EnBolivia(Manana, 16),
            direccion: "  Av. Banzer, 4to anillo  ", descuento: 5)).LeerJsonAsync();

        var linea = Assert.Single(pedido.GetProperty("detalles").EnumerateArray());
        Assert.Equal(otroProducto, linea.GetProperty("productoId").GetInt32());
        Assert.Equal(30m, pedido.GetProperty("subTotal").GetDecimal());
        Assert.Equal(25m, pedido.GetProperty("total").GetDecimal());
        Assert.Equal("Av. Banzer, 4to anillo", pedido.GetProperty("direccionEntrega").GetString());
        Assert.Equal("Llamar antes de llegar", pedido.GetProperty("observaciones").GetString());
        Assert.Equal(Manana.ToDateTime(new TimeOnly(20, 0)), pedido.GetProperty("fechaEntregaEstimada").GetDateTime());
        Assert.Equal(clienteId, pedido.GetProperty("cliente").GetProperty("id").GetInt32());
        // Modificar no cambia el estado.
        Assert.Equal(1, EstadoDe(pedido));
        var entrada = pedido.GetProperty("historial").EnumerateArray().Last();
        Assert.Equal("Pedido modificado", entrada.GetProperty("observacion").GetString());
        Assert.Equal(1, entrada.GetProperty("estado").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Un_pedido_en_ruta_o_anulado_no_se_modifica()
    {
        using var operador = await api.OperadorAsync();
        var productoId = await operador.CrearProductoAsync(10m);
        var (enRuta, _) = await PedidoNuevoAsync(operador);
        await PasarAEnRutaAsync(operador, enRuta);
        var (anulado, _) = await PedidoNuevoAsync(operador);
        (await operador.PostAsJsonAsync($"/api/pedidos/{anulado}/anular", new { motivo = "Prueba" })).EnsureSuccessStatusCode();

        var problema = await (await ModificarAsync(operador, enRuta, productoId)).ProblemaAsync(400, "pedido_no_modificable");
        Assert.EndsWith("está en ruta: solo se modifica mientras está pendiente.", problema.GetProperty("detail").GetString());
        await (await ModificarAsync(operador, anulado, productoId)).ProblemaAsync(400, "pedido_no_modificable");
    }

    [Fact]
    public async Task Modificar_un_pedido_atrasado_sin_tocar_su_fecha_se_permite_pero_no_moverlo_a_otra_fecha_pasada()
    {
        using var operador = await api.OperadorAsync();
        var (pedidoId, _) = await PedidoNuevoAsync(operador);
        var productoId = await operador.CrearProductoAsync(10m);
        var ayer = DateTime.UtcNow.AddDays(-1);
        ayer = new DateTime(ayer.Year, ayer.Month, ayer.Day, ayer.Hour, 0, 0, DateTimeKind.Utc);
        using (var scope = api.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Pedidos.Where(p => p.Id == pedidoId)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.FechaEntregaEstimada, ayer));

        // La misma fecha, enviada en UTC: se corrige el pedido sin reprogramarlo.
        Assert.Equal(HttpStatusCode.OK,
            (await ModificarAsync(operador, pedidoId, productoId, fecha: ayer.ToString("yyyy-MM-ddTHH:mm:ssZ"))).StatusCode);
        await (await ModificarAsync(operador, pedidoId, productoId, fecha: ayer.AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ssZ")))
            .ProblemaAsync(400, "fecha_en_el_pasado");
    }

    [Fact]
    public async Task Modificar_valida_lo_mismo_que_registrar()
    {
        using var operador = await api.OperadorAsync();
        var (pedidoId, _) = await PedidoNuevoAsync(operador);
        var productoId = await operador.CrearProductoAsync(10m);

        await (await ModificarAsync(operador, pedidoId, productoId, descuento: 50)).ProblemaAsync(400, "total_invalido");
        var problema = await (await ModificarAsync(operador, pedidoId, productoId, direccion: "")).ProblemaAsync(400, "validacion");
        Assert.True(problema.GetProperty("errors").TryGetProperty("direccionEntrega", out _));
    }

    [Fact]
    public async Task Reprogramar_vuelve_a_pendiente_con_la_nueva_fecha_y_el_motivo_en_el_historial()
    {
        using var operador = await api.OperadorAsync();
        var (pedidoId, _) = await PedidoNuevoAsync(operador);
        await PasarAEnRutaAsync(operador, pedidoId);
        var pasadoManana = Manana.AddDays(1);

        var pedido = await (await ReprogramarAsync(operador, pedidoId, EnBolivia(pasadoManana, 9, 30), "  El cliente viaja  "))
            .LeerJsonAsync();

        Assert.Equal(1, EstadoDe(pedido));
        Assert.Equal(pasadoManana.ToDateTime(new TimeOnly(13, 30)), pedido.GetProperty("fechaEntregaEstimada").GetDateTime());
        var entrada = pedido.GetProperty("historial").EnumerateArray().Last();
        Assert.Equal(1, entrada.GetProperty("estado").GetProperty("id").GetInt32());
        Assert.Equal($"Entrega reprogramada para el {pasadoManana:dd/MM/yyyy} 09:30. Motivo: El cliente viaja",
            entrada.GetProperty("observacion").GetString());
    }

    [Fact]
    public async Task Reprogramar_pide_motivo_y_una_fecha_futura_y_no_aplica_a_un_pedido_anulado()
    {
        using var operador = await api.OperadorAsync();
        var (pedidoId, _) = await PedidoNuevoAsync(operador);
        var ayer = HoraBolivia.Hoy(TimeProvider.System).AddDays(-1);

        var problema = await (await ReprogramarAsync(operador, pedidoId, null, motivo: "")).ProblemaAsync(400, "validacion");
        Assert.True(problema.GetProperty("errors").TryGetProperty("motivo", out _));
        Assert.True(problema.GetProperty("errors").TryGetProperty("fechaEntregaEstimada", out _));
        await (await ReprogramarAsync(operador, pedidoId, EnBolivia(ayer, 10))).ProblemaAsync(400, "fecha_en_el_pasado");

        (await operador.PostAsJsonAsync($"/api/pedidos/{pedidoId}/anular", new { motivo = "Prueba" })).EnsureSuccessStatusCode();
        await (await ReprogramarAsync(operador, pedidoId, EnBolivia(Manana, 10))).ProblemaAsync(400, "pedido_en_estado_final");
    }

    [Fact]
    public async Task La_secretaria_modifica_y_reprograma_y_el_promotor_no()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var (pedidoId, _) = await PedidoNuevoAsync(operador, await operador.CrearZonaAsync(promotor.Id));
        var productoId = await operador.CrearProductoAsync(10m);
        using var clientePromotor = api.ClienteCon(promotor.Token);
        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);

        await (await ModificarAsync(clientePromotor, pedidoId, productoId)).ProblemaAsync(403, "sin_permiso");
        await (await ReprogramarAsync(clientePromotor, pedidoId, EnBolivia(Manana, 11))).ProblemaAsync(403, "sin_permiso");
        Assert.Equal(HttpStatusCode.OK, (await ModificarAsync(secretaria, pedidoId, productoId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ReprogramarAsync(secretaria, pedidoId, EnBolivia(Manana, 11))).StatusCode);
    }
}
