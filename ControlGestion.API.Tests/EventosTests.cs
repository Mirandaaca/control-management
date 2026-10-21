using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class EventosTests(ApiDePrueba api)
{
    [Fact]
    public async Task Registrar_numera_el_evento()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());

        var respuesta = await operador.PostAsJsonAsync("/api/eventos", new
        {
            tipoEventoId = 3,
            clienteId,
            latitud = -17.7833m,
            longitud = -63.1821m,
            descripcion = "El portón estaba cerrado"
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var evento = await respuesta.LeerJsonAsync();
        Assert.Matches(new Regex($@"^EVT-{DateTime.UtcNow.AddHours(-4).Year}-\d{{5}}$"), evento.GetProperty("numero").GetString());
        var id = evento.GetProperty("id").GetInt32();
        Assert.Equal(id, (await (await operador.GetAsync($"/api/eventos/{id}")).LeerJsonAsync()).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Un_evento_que_no_existe_responde_404()
    {
        using var operador = await api.OperadorAsync();

        await (await operador.GetAsync("/api/eventos/999999")).ProblemaAsync(404, "no_encontrado");
    }

    [Fact]
    public async Task El_catalogo_trae_los_cinco_tipos_y_cuales_piden_evidencia()
    {
        using var operador = await api.OperadorAsync();

        var tipos = (await (await operador.GetAsync("/api/eventos/tipos")).LeerJsonAsync()).EnumerateArray()
            .Select(t => (t.GetProperty("id").GetInt32(), t.GetProperty("requiereEvidencia").GetBoolean())).ToList();

        Assert.Equal([(1, true), (2, true), (3, false), (4, false), (5, false)], tipos);
    }

    [Fact]
    public async Task El_promotor_no_resuelve_y_la_secretaria_no_registra()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id));
        using var clientePromotor = api.ClienteCon(promotor.Token);
        using var clienteSecretaria = api.ClienteCon(secretaria.Token);
        var eventoId = await clientePromotor.CrearEventoAsync(clienteId);

        await (await clientePromotor.PostAsJsonAsync($"/api/eventos/{eventoId}/resolver", new { solucion = "Listo" }))
            .ProblemaAsync(403, "sin_permiso");
        await (await clienteSecretaria.PostAsJsonAsync("/api/eventos", new
        {
            tipoEventoId = 3, clienteId, latitud = -17.7833m, longitud = -63.1821m, descripcion = "No debería entrar"
        })).ProblemaAsync(403, "sin_permiso");
    }
}
