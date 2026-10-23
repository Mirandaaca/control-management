using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Data;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class ErroresTests(ApiDePrueba api)
{
    [Fact]
    public async Task La_validacion_responde_cada_campo_en_camelCase_y_en_espanol()
    {
        using var admin = await api.AdminAsync();
        var problema = await (await admin.PostAsJsonAsync("/api/usuarios",
            new { email = "no-es-correo", nombreCompleto = "", password = "abcdef", rol = "" })).ProblemaAsync(400, "validacion");

        var errores = problema.GetProperty("errors");
        foreach (var campo in new[] { "email", "nombreCompleto", "password", "rol" })
        {
            Assert.True(errores.TryGetProperty(campo, out var mensajes), $"Falta errors.{campo}.");
            Assert.All(mensajes.EnumerateArray(), m => Assert.DoesNotContain("The ", m.GetString()));
        }
    }

    [Fact]
    public async Task Un_JSON_con_un_valor_invalido_no_muestra_detalles_internos()
    {
        using var operador = await api.OperadorAsync();
        var respuesta = await operador.PostAsJsonAsync("/api/cobranzas/1/pagos", new { monto = 1, metodoPago = "Tarjeta" });

        var problema = await respuesta.ProblemaAsync(400, "validacion");
        var mensaje = problema.GetProperty("errors").GetProperty("metodoPago")[0].GetString();
        Assert.Equal("El valor de 'metodoPago' no es válido.", mensaje);
        Assert.False(problema.GetProperty("errors").TryGetProperty("dto", out _));
    }

    [Fact]
    public async Task Una_referencia_a_un_id_que_no_existe_responde_400_y_no_500()
    {
        using var operador = await api.OperadorAsync();
        await (await operador.PostAsJsonAsync("/api/rutas", new
        {
            zonaId = 999999,
            promotorId = ApiDePrueba.PromotorDeZonasId,
            fecha = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"),
            clientes = new[] { 1 }
        })).ProblemaAsync(400, "validacion");

        // Desde la tarea 14 ningún endpoint llega a esta FK sin revisar antes el id (Rutas era el último). Se provoca el
        // error 1452 de verdad, con una zona que no existe, y se pasa por el manejador.
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Rutas.Add(new Ruta
        {
            Numero = Ayudantes.Unico("R"),
            PromotorId = ApiDePrueba.PromotorDeZonasId,
            ZonaId = 999999,
            FechaPlanificada = new DateOnly(2000, 1, 1),
        });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        http.Response.Body = new MemoryStream();

        var manejador = ActivatorUtilities.CreateInstance<ManejadorDeExcepciones>(scope.ServiceProvider);
        Assert.True(await manejador.TryHandleAsync(http, error, CancellationToken.None));

        Assert.Equal(400, http.Response.StatusCode);
        http.Response.Body.Position = 0;
        var problema = (await JsonDocument.ParseAsync(http.Response.Body)).RootElement;
        Assert.Equal("referencia_invalida", problema.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Un_registro_con_datos_asociados_que_llega_a_la_FK_responde_409_y_no_500()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        await operador.CrearClienteAsync(zonaId);
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Ningún endpoint llega ya a esta FK: zonas, estados del cliente y roles revisan el uso antes de borrar. Se
        // provoca el error 1451 de verdad, borrando la zona sin pasar por su servicio, y se pasa por el manejador.
        db.Zonas.Remove(await db.Zonas.SingleAsync(z => z.Id == zonaId));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        http.Response.Body = new MemoryStream();

        var manejador = ActivatorUtilities.CreateInstance<ManejadorDeExcepciones>(scope.ServiceProvider);
        Assert.True(await manejador.TryHandleAsync(http, error, CancellationToken.None));

        Assert.Equal(409, http.Response.StatusCode);
        http.Response.Body.Position = 0;
        var problema = (await JsonDocument.ParseAsync(http.Response.Body)).RootElement;
        Assert.Equal("tiene_registros_asociados", problema.GetProperty("codigo").GetString());
        Assert.Equal("No se puede borrar porque tiene registros asociados.", problema.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Un_registro_o_una_ruta_que_no_existen_responden_404_con_ProblemDetails()
    {
        using var operador = await api.OperadorAsync();
        await (await operador.GetAsync("/api/clientes/999999")).ProblemaAsync(404, "no_encontrado");
        await (await operador.GetAsync("/api/no-existe")).ProblemaAsync(404, "no_encontrado");
    }
}
