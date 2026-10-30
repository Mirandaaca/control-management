using ControlGestion.API.Data;
using ControlGestion.API.Models;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>
/// La migración QuitarEstadoEnProceso (tarea 27), con un pedido que estaba En proceso. Baja la base de la prueba a la
/// migración anterior y la vuelve a subir: las pruebas de la colección "Api" corren de a una, así ninguna la ve a medio
/// camino, y el finally la deja siempre al día.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class QuitarEstadoEnProcesoTests(ApiDePrueba api)
{
    private const string MigracionAnterior = "20261024170251_UnaCobranzaPorHojaDePedido";
    private const int EnProceso = 2;
    private const string Nota = "Estaba En proceso (estado quitado el 30/10/2026).";

    [Fact]
    public async Task Los_pedidos_en_proceso_quedan_pendientes_y_su_historial_lo_anota()
    {
        using var operador = await api.OperadorAsync();
        var pedidoId = await operador.CrearPedidoAsync(await operador.CrearClienteAsync(await operador.CrearZonaAsync()),
            await operador.CrearProductoAsync(10m), 1);

        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var migrador = db.GetService<IMigrator>();
        try
        {
            // El Down vuelve a crear el estado 2: el pedido pasa a En proceso y se modifica en ese estado.
            await migrador.MigrateAsync(MigracionAnterior);
            var usuarioId = await db.HistorialEstadosPedido.Where(h => h.PedidoId == pedidoId).Select(h => h.UsuarioId).SingleAsync();
            await db.Pedidos.Where(p => p.Id == pedidoId).ExecuteUpdateAsync(p => p.SetProperty(x => x.EstadoPedidoId, EnProceso));
            db.HistorialEstadosPedido.AddRange(
                new HistorialEstadoPedido { PedidoId = pedidoId, EstadoPedidoId = EnProceso, UsuarioId = usuarioId },
                new HistorialEstadoPedido
                {
                    PedidoId = pedidoId, EstadoPedidoId = EnProceso, UsuarioId = usuarioId, Observacion = "Pedido modificado"
                });
            await db.SaveChangesAsync();
        }
        finally
        {
            await migrador.MigrateAsync();
        }

        var pedido = await (await operador.GetAsync($"/api/pedidos/{pedidoId}")).LeerJsonAsync();
        Assert.Equal(EstadosDePedido.Pendiente, pedido.GetProperty("estado").GetProperty("id").GetInt32());
        Assert.Equal([
                (EstadosDePedido.Pendiente, "Pedido registrado"),
                (EstadosDePedido.Pendiente, Nota),
                (EstadosDePedido.Pendiente, $"{Nota} Pedido modificado"),
            ],
            pedido.GetProperty("historial").EnumerateArray()
                .Select(h => (h.GetProperty("estado").GetProperty("id").GetInt32(), h.GetProperty("observacion").GetString()))
                .ToList());
        var estados = (await (await operador.GetAsync("/api/pedidos/estados")).LeerJsonAsync()).EnumerateArray()
            .Select(e => e.GetProperty("id").GetInt32()).ToList();
        Assert.Equal([1, 3, 4, 5], estados);
    }
}
