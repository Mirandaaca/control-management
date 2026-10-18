using System.Net.Http.Json;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class ErroresTests(ApiDePrueba api)
{
    [Fact]
    public async Task La_validacion_responde_cada_campo_en_camelCase_y_en_espanol()
    {
        using var admin = await api.AdminAsync();
        var problema = await (await admin.PostAsJsonAsync("/api/usuarios",
            new { email = "no-es-correo", nombreCompleto = "", password = "abcdef", rol = "Root" })).ProblemaAsync(400, "validacion");

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
        using var admin = await api.AdminAsync();
        var respuesta = await admin.PostAsJsonAsync("/api/cobranzas/1/pagos", new { monto = 1, metodoPago = "QR" });

        var problema = await respuesta.ProblemaAsync(400, "validacion");
        var mensaje = problema.GetProperty("errors").GetProperty("metodoPago")[0].GetString();
        Assert.Equal("El valor de 'metodoPago' no es válido.", mensaje);
        Assert.False(problema.GetProperty("errors").TryGetProperty("dto", out _));
    }

    [Fact]
    public async Task Una_referencia_a_un_id_que_no_existe_responde_400_y_no_500()
    {
        using var admin = await api.AdminAsync();
        var zonaId = await admin.CrearZonaAsync();
        var productoId = await admin.CrearProductoAsync(1m);

        await (await admin.PostAsJsonAsync("/api/pedidos", new
        {
            clienteId = 999999,
            zonaId,
            detalles = new[] { new { productoId, cantidad = 1 } }
        })).ProblemaAsync(400, "referencia_invalida");
    }

    [Fact]
    public async Task Borrar_una_zona_con_clientes_responde_409_y_no_500()
    {
        using var admin = await api.AdminAsync();
        var zonaId = await admin.CrearZonaAsync();
        await admin.CrearClienteAsync(zonaId);

        await (await admin.DeleteAsync($"/api/zonas/{zonaId}")).ProblemaAsync(409, "tiene_registros_asociados");
    }

    [Fact]
    public async Task Un_registro_o_una_ruta_que_no_existen_responden_404_con_ProblemDetails()
    {
        using var admin = await api.AdminAsync();
        await (await admin.GetAsync("/api/clientes/999999")).ProblemaAsync(404, "no_encontrado");
        await (await admin.GetAsync("/api/no-existe")).ProblemaAsync(404, "no_encontrado");
    }
}
