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
public class PedidosDistribucionTests(ApiDePrueba api)
{
    private static DateOnly Hoy => HoraBolivia.Hoy(TimeProvider.System);

    private static async Task<JsonElement> PedidoDe(HttpClient cliente, int pedidoId) =>
        await (await cliente.GetAsync($"/api/pedidos/{pedidoId}")).LeerJsonAsync();

    private static async Task<List<int>> IdsAsync(HttpClient cliente, string consulta) =>
        (await (await cliente.GetAsync($"/api/pedidos{consulta}")).LeerJsonAsync())
            .GetProperty("items").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).ToList();

    private static Task<HttpResponseMessage> AsignarAsync(HttpClient cliente, string promotorId, DateOnly? dia, params int[] pedidoIds) =>
        cliente.PostAsJsonAsync("/api/pedidos/asignar", new { pedidoIds, promotorId, dia = dia?.ToString("yyyy-MM-dd") });

    private static Task<HttpResponseMessage> ReprogramarAsync(HttpClient cliente, int pedidoId, DateOnly dia, int hora, int minuto = 0) =>
        cliente.PostAsJsonAsync($"/api/pedidos/{pedidoId}/reprogramar",
            new { fechaEntregaEstimada = $"{dia:yyyy-MM-dd}T{hora:00}:{minuto:00}:00", motivo = "Ajuste de la prueba" });

    private static async Task<int> PedidoNuevoAsync(HttpClient operador, int? zonaId = null) =>
        await operador.CrearPedidoAsync(await operador.CrearClienteAsync(zonaId ?? await operador.CrearZonaAsync()),
            await operador.CrearProductoAsync(10m), 1);

    [Fact]
    public async Task Asignar_pone_el_promotor_y_el_dia_y_cada_pedido_conserva_su_hora_y_su_estado()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var deLasDiez = await PedidoNuevoAsync(operador);
        var deLaTarde = await PedidoNuevoAsync(operador);
        (await ReprogramarAsync(operador, deLaTarde, Hoy.AddDays(2), 15, 30)).EnsureSuccessStatusCode();
        (await operador.PostAsJsonAsync($"/api/pedidos/{deLaTarde}/avanzar", new { })).EnsureSuccessStatusCode();
        var dia = Hoy.AddDays(3);

        var asignados = (await (await AsignarAsync(operador, promotor.Id, dia, deLaTarde, deLasDiez)).LeerJsonAsync())
            .EnumerateArray().ToList();

        // Vuelven ordenados por hora de entrega: 10:00 y 15:30 de Bolivia, que son 14:00 y 19:30 en UTC.
        Assert.Equal([deLasDiez, deLaTarde], asignados.Select(p => p.GetProperty("id").GetInt32()));
        Assert.Equal([dia.ToDateTime(new TimeOnly(14, 0)), dia.ToDateTime(new TimeOnly(19, 30))],
            asignados.Select(p => p.GetProperty("fechaEntregaEstimada").GetDateTime()));
        Assert.All(asignados, p => Assert.Equal(promotor.Id, p.GetProperty("repartidor").GetProperty("id").GetString()));
        Assert.Equal([1, 2], asignados.Select(p => p.GetProperty("estado").GetProperty("id").GetInt32()));

        var entrada = (await PedidoDe(operador, deLasDiez)).GetProperty("historial").EnumerateArray().Last();
        Assert.Equal($"Asignado a Prueba {Roles.Promotor} para el {dia:dd/MM/yyyy}.", entrada.GetProperty("observacion").GetString());
        Assert.Equal(1, entrada.GetProperty("estado").GetProperty("id").GetInt32());

        Assert.Equal([deLaTarde, deLasDiez], await IdsAsync(operador, $"?repartidorId={promotor.Id}"));
        var sinAsignar = await IdsAsync(operador, $"?sinAsignar=true&desde={dia:yyyy-MM-dd}&hasta={dia:yyyy-MM-dd}&tamano=100");
        Assert.DoesNotContain(deLasDiez, sinAsignar);
        Assert.DoesNotContain(deLaTarde, sinAsignar);
        var otro = await PedidoNuevoAsync(operador);
        Assert.Contains(otro, await IdsAsync(operador, "?sinAsignar=true&tamano=100"));
    }

    [Fact]
    public async Task El_promotor_asignado_ve_el_pedido_aunque_el_cliente_sea_de_otra_cartera()
    {
        using var operador = await api.OperadorAsync();
        var promotorA = await api.CrearUsuarioAsync(Roles.Promotor);
        var promotorB = await api.CrearUsuarioAsync(Roles.Promotor);
        var pedidoDeB = await PedidoNuevoAsync(operador, await operador.CrearZonaAsync(promotorB.Id));
        using var clienteA = api.ClienteCon(promotorA.Token);
        await (await clienteA.GetAsync($"/api/pedidos/{pedidoDeB}")).ProblemaAsync(404, "no_encontrado");

        (await AsignarAsync(operador, promotorA.Id, Hoy.AddDays(1), pedidoDeB)).EnsureSuccessStatusCode();

        Assert.Equal([pedidoDeB], await IdsAsync(clienteA, ""));
        Assert.Equal(HttpStatusCode.OK, (await clienteA.GetAsync($"/api/pedidos/{pedidoDeB}")).StatusCode);
    }

    [Fact]
    public async Task Solo_se_asigna_a_un_promotor_activo()
    {
        using var operador = await api.OperadorAsync();
        var pedidoId = await PedidoNuevoAsync(operador);
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        var inactivo = await api.CrearUsuarioAsync(Roles.Promotor);
        using (var admin = await api.AdminAsync())
            (await admin.PutAsync($"/api/usuarios/{inactivo.Id}/desactivar", null)).EnsureSuccessStatusCode();

        foreach (var promotorId in new[] { secretaria.Id, inactivo.Id, "no-existe" })
            await (await AsignarAsync(operador, promotorId, Hoy.AddDays(1), pedidoId)).ProblemaAsync(400, "promotor_no_valido");
        Assert.Equal(JsonValueKind.Null, (await PedidoDe(operador, pedidoId)).GetProperty("repartidor").ValueKind);
    }

    [Fact]
    public async Task Si_un_pedido_no_se_puede_asignar_no_se_asigna_ninguno()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var bueno = await PedidoNuevoAsync(operador);
        var anulado = await PedidoNuevoAsync(operador);
        (await operador.PostAsJsonAsync($"/api/pedidos/{anulado}/anular", new { motivo = "Prueba" })).EnsureSuccessStatusCode();
        var original = (await PedidoDe(operador, bueno)).GetProperty("fechaEntregaEstimada").GetDateTime();

        await (await AsignarAsync(operador, promotor.Id, Hoy.AddDays(2), bueno, anulado)).ProblemaAsync(400, "pedido_en_estado_final");

        var sinCambios = await PedidoDe(operador, bueno);
        Assert.Equal(JsonValueKind.Null, sinCambios.GetProperty("repartidor").ValueKind);
        Assert.Equal(original, sinCambios.GetProperty("fechaEntregaEstimada").GetDateTime());
    }

    [Fact]
    public async Task Un_pedido_cuya_hora_ya_paso_ese_dia_no_se_asigna()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var pedidoId = await PedidoNuevoAsync(operador);
        // A las 00:00 de Bolivia: hoy a esa hora ya pasó.
        (await ReprogramarAsync(operador, pedidoId, Hoy.AddDays(1), 0)).EnsureSuccessStatusCode();

        await (await AsignarAsync(operador, promotor.Id, Hoy, pedidoId)).ProblemaAsync(400, "fecha_en_el_pasado");
    }

    [Fact]
    public async Task Un_pedido_sin_fecha_de_entrega_no_se_asigna()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var pedidoId = await PedidoNuevoAsync(operador);
        // Solo los pedidos que había antes de la tarea 08 pueden quedar sin fecha.
        using (var scope = api.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Pedidos.Where(p => p.Id == pedidoId)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.FechaEntregaEstimada, (DateTime?)null));

        await (await AsignarAsync(operador, promotor.Id, Hoy.AddDays(1), pedidoId)).ProblemaAsync(400, "pedido_sin_fecha");
    }

    [Fact]
    public async Task Reprogramar_conserva_al_promotor_asignado()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var pedidoId = await PedidoNuevoAsync(operador);
        (await AsignarAsync(operador, promotor.Id, Hoy.AddDays(1), pedidoId)).EnsureSuccessStatusCode();

        var pedido = await (await ReprogramarAsync(operador, pedidoId, Hoy.AddDays(4), 11)).LeerJsonAsync();

        Assert.Equal(promotor.Id, pedido.GetProperty("repartidor").GetProperty("id").GetString());
    }

    [Fact]
    public async Task Asignar_valida_los_datos_y_lo_hace_la_secretaria_y_no_el_promotor()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var pedidoId = await PedidoNuevoAsync(operador, await operador.CrearZonaAsync(promotor.Id));
        using var clientePromotor = api.ClienteCon(promotor.Token);
        using var secretaria = api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);

        var problema = await (await AsignarAsync(secretaria, promotor.Id, null)).ProblemaAsync(400, "validacion");
        Assert.True(problema.GetProperty("errors").TryGetProperty("pedidoIds", out _));
        Assert.True(problema.GetProperty("errors").TryGetProperty("dia", out _));
        await (await AsignarAsync(clientePromotor, promotor.Id, Hoy.AddDays(1), pedidoId)).ProblemaAsync(403, "sin_permiso");
        Assert.Equal(HttpStatusCode.OK, (await AsignarAsync(secretaria, promotor.Id, Hoy.AddDays(1), pedidoId)).StatusCode);
    }
}
