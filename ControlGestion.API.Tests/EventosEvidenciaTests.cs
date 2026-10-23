using System.Net;
using System.Net.Http.Json;
using ControlGestion.API.Data;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlGestion.API.Tests;

/// <summary>Eliminar fotos y resolver con la evidencia que pide el tipo (T7.07, T7.10 y T7.11).</summary>
[Collection(ColeccionApi.Nombre)]
public class EventosEvidenciaTests(ApiDePrueba api)
{
    private const int ClienteAusente = 1;

    private static Task<HttpResponseMessage> ResolverAsync(HttpClient cliente, int eventoId) =>
        cliente.PostAsJsonAsync($"/api/eventos/{eventoId}/resolver", new { solucion = "Se volvió a visitar" });

    private static async Task<int> SubirAsync(HttpClient cliente, int eventoId) =>
        (await (await cliente.SubirFotoAsync(eventoId)).LeerJsonAsync())
            .GetProperty("fotos").EnumerateArray().Last().GetProperty("id").GetInt32();

    private async Task<int> EventoAsync(HttpClient operador, int tipoEventoId = 3) =>
        await operador.CrearEventoAsync(await operador.CrearClienteAsync(await operador.CrearZonaAsync()), tipoEventoId);

    [Fact]
    public async Task Eliminar_quita_la_foto_y_su_archivo()
    {
        using var operador = await api.OperadorAsync();
        var eventoId = await EventoAsync(operador);
        var fotoId = await SubirAsync(operador, eventoId);
        string archivo;
        using (var scope = api.Services.CreateScope())
            archivo = Path.Combine(api.CarpetaDeArchivos, await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                .Evidencias.Where(f => f.Id == fotoId).Select(f => f.RutaAlmacenamiento).SingleAsync());
        Assert.True(File.Exists(archivo));

        var evento = await (await operador.DeleteAsync($"/api/eventos/{eventoId}/fotos/{fotoId}")).LeerJsonAsync();

        Assert.Empty(evento.GetProperty("fotos").EnumerateArray());
        Assert.False(File.Exists(archivo));
        await (await operador.GetAsync($"/api/evidencias/{fotoId}/archivo")).ProblemaAsync(404, "no_encontrado");
    }

    [Fact]
    public async Task No_se_elimina_una_foto_de_un_evento_resuelto_ni_la_de_otro_evento()
    {
        using var operador = await api.OperadorAsync();
        var eventoId = await EventoAsync(operador);
        var fotoId = await SubirAsync(operador, eventoId);
        var otroId = await EventoAsync(operador);

        await (await operador.DeleteAsync($"/api/eventos/{otroId}/fotos/{fotoId}")).ProblemaAsync(404, "no_encontrado");
        (await ResolverAsync(operador, eventoId)).EnsureSuccessStatusCode();
        await (await operador.DeleteAsync($"/api/eventos/{eventoId}/fotos/{fotoId}")).ProblemaAsync(400, "evento_resuelto");
    }

    [Fact]
    public async Task El_promotor_no_elimina_fotos_del_evento_que_registro_otro()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var eventoId = await operador.CrearEventoAsync(await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id)));
        var fotoId = await SubirAsync(operador, eventoId);
        using var cliente = api.ClienteCon(promotor.Token);

        await (await cliente.DeleteAsync($"/api/eventos/{eventoId}/fotos/{fotoId}")).ProblemaAsync(400, "evento_de_otro_promotor");
    }

    [Fact]
    public async Task Un_tipo_que_pide_evidencia_no_se_resuelve_sin_una_foto()
    {
        using var operador = await api.OperadorAsync();
        // T7.10 y T7.11: un evento "Cliente ausente" sin foto se resolvía igual.
        var eventoId = await EventoAsync(operador, ClienteAusente);

        var problema = await (await ResolverAsync(operador, eventoId)).ProblemaAsync(400, "evento_sin_evidencia");
        Assert.Contains("Cliente ausente", problema.GetProperty("detail").GetString());

        await SubirAsync(operador, eventoId);
        Assert.Equal(HttpStatusCode.OK, (await ResolverAsync(operador, eventoId)).StatusCode);
    }

    [Fact]
    public async Task Quitar_la_unica_foto_mientras_se_resuelve_nunca_deja_un_resuelto_sin_fotos()
    {
        using var operador = await api.OperadorAsync();
        for (var vuelta = 0; vuelta < 5; vuelta++)
        {
            var eventoId = await EventoAsync(operador, ClienteAusente);
            var fotoId = await SubirAsync(operador, eventoId);

            var respuestas = await Ayudantes.EnviarALaVezAsync(2, i => i == 0
                ? operador.DeleteAsync($"/api/eventos/{eventoId}/fotos/{fotoId}")
                : ResolverAsync(operador, eventoId));

            Assert.All(respuestas, r => Assert.Contains(r.StatusCode,
                new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Conflict }));
            var evento = await (await operador.GetAsync($"/api/eventos/{eventoId}")).LeerJsonAsync();
            if (evento.GetProperty("estado").GetInt32() == 1)
                Assert.NotEmpty(evento.GetProperty("fotos").EnumerateArray());
        }
    }
}
