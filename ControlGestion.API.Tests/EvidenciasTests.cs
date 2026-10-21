using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class EvidenciasTests(ApiDePrueba api)
{
    /// <summary>Un evento Pendiente registrado por un promotor en un cliente de su cartera.</summary>
    private async Task<(int EventoId, HttpClient Promotor)> EventoDeUnPromotorAsync()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id));
        var cliente = api.ClienteCon(promotor.Token);
        return (await cliente.CrearEventoAsync(clienteId), cliente);
    }

    private static async Task<List<JsonElement>> FotosAsync(HttpClient cliente, int eventoId) =>
        (await (await cliente.GetAsync($"/api/eventos/{eventoId}")).LeerJsonAsync()).GetProperty("fotos").EnumerateArray().ToList();

    [Fact]
    public async Task Sube_JPEG_PNG_y_WebP_y_cada_una_se_ve_con_el_tipo_de_su_firma()
    {
        var (eventoId, promotor) = await EventoDeUnPromotorAsync();
        using var _p = promotor;
        var png = Ayudantes.Png();

        // Un PNG que se declara JPEG: manda la firma, no lo que dice quien lo sube.
        (await promotor.SubirFotoAsync(eventoId, png, "foto.jpg", "image/jpeg")).EnsureSuccessStatusCode();
        (await promotor.SubirFotoAsync(eventoId, Ayudantes.Jpeg(), "foto.jpg", "image/jpeg")).EnsureSuccessStatusCode();
        var evento = await (await promotor.SubirFotoAsync(eventoId, Ayudantes.Webp(), "foto.webp", "image/webp")).LeerJsonAsync();

        var fotos = evento.GetProperty("fotos").EnumerateArray().ToList();
        Assert.Equal(3, fotos.Count);
        Assert.Equal("foto.jpg", fotos[0].GetProperty("nombreArchivo").GetString());
        Assert.Equal("Prueba Promotor", fotos[0].GetProperty("subidoPor").GetProperty("nombreCompleto").GetString());
        var tipos = new List<string?>();
        foreach (var foto in fotos)
        {
            var respuesta = await promotor.GetAsync($"/api/evidencias/{foto.GetProperty("id").GetInt32()}/archivo");
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.True(respuesta.Headers.CacheControl?.Private);
            tipos.Add(respuesta.Content.Headers.ContentType?.MediaType);
        }
        Assert.Equal(["image/png", "image/jpeg", "image/webp"], tipos);
        Assert.Equal(png, await (await promotor.GetAsync($"/api/evidencias/{fotos[0].GetProperty("id").GetInt32()}/archivo"))
            .Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Un_archivo_que_no_es_imagen_se_rechaza_aunque_diga_que_es_PNG()
    {
        var (eventoId, promotor) = await EventoDeUnPromotorAsync();
        using var _p = promotor;
        var html = "<html><body><script>alert(1)</script></body></html>"u8.ToArray();

        // T7.05: este .html se guardaba y se servía como página, sin token.
        await (await promotor.SubirFotoAsync(eventoId, html, "foto.png", "image/png")).ProblemaAsync(400, "archivo_no_es_imagen");
        Assert.Empty(await FotosAsync(promotor, eventoId));
        Assert.Empty(Directory.Exists(api.CarpetaDeArchivos)
            ? Directory.GetFiles(api.CarpetaDeArchivos, "*.html", SearchOption.AllDirectories) : []);
    }

    [Fact]
    public async Task Una_foto_de_mas_de_10_MB_se_rechaza_diciendo_el_limite()
    {
        var (eventoId, promotor) = await EventoDeUnPromotorAsync();
        using var _p = promotor;

        var problema = await (await promotor.SubirFotoAsync(eventoId, Ayudantes.Jpeg(12 * 1024 * 1024), "grande.jpg", "image/jpeg"))
            .ProblemaAsync(400, "archivo_muy_grande");

        Assert.Equal("La foto pesa 12 MB; el máximo es 10 MB.", problema.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Un_evento_tiene_hasta_5_fotos_y_uno_resuelto_no_recibe_mas()
    {
        var (eventoId, promotor) = await EventoDeUnPromotorAsync();
        using var _p = promotor;
        for (var i = 0; i < 5; i++) (await promotor.SubirFotoAsync(eventoId)).EnsureSuccessStatusCode();

        await (await promotor.SubirFotoAsync(eventoId)).ProblemaAsync(400, "limite_de_fotos");

        using var operador = await api.OperadorAsync();
        var otroId = await operador.CrearEventoAsync(await operador.CrearClienteAsync(await operador.CrearZonaAsync()));
        (await operador.PostAsJsonAsync($"/api/eventos/{otroId}/resolver", new { solucion = "Listo" })).EnsureSuccessStatusCode();
        await (await operador.SubirFotoAsync(otroId)).ProblemaAsync(400, "evento_resuelto");
    }

    [Fact]
    public async Task Sin_archivo_responde_el_error_en_el_campo()
    {
        var (eventoId, promotor) = await EventoDeUnPromotorAsync();
        using var _p = promotor;

        var formulario = new MultipartFormDataContent { { new StringContent("2026-10-21T14:30:00Z"), "fechaCaptura" } };
        var problema = await (await promotor.PostAsync($"/api/eventos/{eventoId}/fotos", formulario)).ProblemaAsync(400, "validacion");

        Assert.True(problema.GetProperty("errors").TryGetProperty("archivo", out _));
    }

    [Fact]
    public async Task La_app_manda_la_ubicacion_y_la_hora_de_la_toma()
    {
        var (eventoId, promotor) = await EventoDeUnPromotorAsync();
        using var _p = promotor;

        var evento = await (await promotor.SubirFotoAsync(eventoId, campos: new Dictionary<string, string>
        {
            ["latitud"] = "-17.78", ["longitud"] = "-63.18", ["fechaCaptura"] = "2026-10-21T14:30:00Z"
        })).LeerJsonAsync();

        var foto = Assert.Single(evento.GetProperty("fotos").EnumerateArray());
        Assert.Equal(-17.78m, foto.GetProperty("latitud").GetDecimal());
        Assert.Equal(new DateTime(2026, 10, 21, 14, 30, 0), foto.GetProperty("fechaCaptura").GetDateTime());
    }

    [Fact]
    public async Task El_promotor_no_agrega_fotos_al_evento_que_registro_otro()
    {
        using var operador = await api.OperadorAsync();
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var eventoId = await operador.CrearEventoAsync(await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id)));
        using var cliente = api.ClienteCon(promotor.Token);

        await (await cliente.SubirFotoAsync(eventoId)).ProblemaAsync(400, "evento_de_otro_promotor");
    }

    [Fact]
    public async Task Una_foto_no_se_abre_sin_sesion_ni_desde_otra_cartera()
    {
        var (eventoId, promotor) = await EventoDeUnPromotorAsync();
        using var _p = promotor;
        var fotoId = (await (await promotor.SubirFotoAsync(eventoId)).LeerJsonAsync())
            .GetProperty("fotos")[0].GetProperty("id").GetInt32();
        var ruta = $"/api/evidencias/{fotoId}/archivo";
        var otroPromotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var gerente = await api.CrearUsuarioAsync(Roles.GerenteGeneral);

        using var anonimo = api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(ruta)).StatusCode);
        using var deOtraCartera = api.ClienteCon(otroPromotor.Token);
        await (await deOtraCartera.GetAsync(ruta)).ProblemaAsync(404, "no_encontrado");
        // El gerente general ve la foto desde el mapa de eventos (ver-mapa), con su cartera completa.
        using var clienteGerente = api.ClienteCon(gerente.Token);
        Assert.Equal(HttpStatusCode.OK, (await clienteGerente.GetAsync(ruta)).StatusCode);
    }
}
