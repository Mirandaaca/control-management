using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

/// <summary>El comprobante del pago como Evidencia (3.1).</summary>
[Collection(ColeccionApi.Nombre)]
public class ComprobantesTests(ApiDePrueba api)
{
    private static Task<HttpResponseMessage> AdjuntarAsync(HttpClient cliente, int cobranzaId, int pagoId, byte[]? contenido = null)
    {
        var archivo = new ByteArrayContent(contenido ?? Ayudantes.Jpeg());
        archivo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        var formulario = new MultipartFormDataContent { { archivo, "archivo", "comprobante.jpg" } };
        return cliente.PutAsync($"/api/cobranzas/{cobranzaId}/pagos/{pagoId}/comprobante", formulario);
    }

    private static async Task<int> PagarAsync(HttpClient cliente, int cobranzaId, decimal monto = 5m) =>
        (await (await cliente.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos", new { monto, metodoPago = 0 })).LeerJsonAsync())
            .GetProperty("id").GetInt32();

    private static async Task<int?> ComprobanteDe(HttpClient cliente, int cobranzaId, int pagoId)
    {
        var pago = (await (await cliente.GetAsync($"/api/cobranzas/{cobranzaId}")).LeerJsonAsync()).GetProperty("pagos")
            .EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == pagoId);
        var comprobante = pago.GetProperty("comprobanteId");
        return comprobante.ValueKind == System.Text.Json.JsonValueKind.Null ? null : comprobante.GetInt32();
    }

    /// <summary>Una cobranza de un cliente de la cartera de un promotor, con un pago sin comprobante.</summary>
    private async Task<(int CobranzaId, int PagoId, UsuarioDePrueba Promotor)> PagoAsync(HttpClient operador)
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id));
        var cobranzaId = await operador.CrearCobranzaAsync(clienteId, await operador.CrearProductoAsync(10m), 2);
        return (cobranzaId, await PagarAsync(operador, cobranzaId), promotor);
    }

    [Fact]
    public async Task El_comprobante_queda_en_el_pago_y_lo_ve_la_cartera_del_cliente()
    {
        using var operador = await api.OperadorAsync();
        var (cobranzaId, pagoId, promotor) = await PagoAsync(operador);
        var contenido = Ayudantes.Jpeg();
        using var clientePromotor = api.ClienteCon(promotor.Token);

        var cobranza = await (await AdjuntarAsync(clientePromotor, cobranzaId, pagoId, contenido)).LeerJsonAsync();

        var comprobanteId = cobranza.GetProperty("pagos")[0].GetProperty("comprobanteId").GetInt32();
        var archivo = await clientePromotor.GetAsync($"/api/evidencias/{comprobanteId}/archivo");
        Assert.Equal("image/jpeg", archivo.Content.Headers.ContentType?.MediaType);
        Assert.Equal(contenido, await archivo.Content.ReadAsByteArrayAsync());
        var otro = await api.CrearUsuarioAsync(Roles.Promotor);
        using var clienteOtro = api.ClienteCon(otro.Token);
        await (await clienteOtro.GetAsync($"/api/evidencias/{comprobanteId}/archivo")).ProblemaAsync(404, "no_encontrado");
        await (await AdjuntarAsync(clienteOtro, cobranzaId, pagoId)).ProblemaAsync(404, "no_encontrado");
    }

    [Fact]
    public async Task Un_comprobante_nuevo_reemplaza_al_anterior()
    {
        using var operador = await api.OperadorAsync();
        var (cobranzaId, pagoId, _) = await PagoAsync(operador);
        (await AdjuntarAsync(operador, cobranzaId, pagoId)).EnsureSuccessStatusCode();
        var anterior = await ComprobanteDe(operador, cobranzaId, pagoId);

        (await AdjuntarAsync(operador, cobranzaId, pagoId, Ayudantes.Png())).EnsureSuccessStatusCode();

        var nuevo = await ComprobanteDe(operador, cobranzaId, pagoId);
        Assert.NotEqual(anterior, nuevo);
        await (await operador.GetAsync($"/api/evidencias/{anterior}/archivo")).ProblemaAsync(404, "no_encontrado");
        Assert.Equal(HttpStatusCode.OK, (await operador.GetAsync($"/api/evidencias/{nuevo}/archivo")).StatusCode);
    }

    [Fact]
    public async Task Un_pago_anulado_no_recibe_comprobante_y_un_archivo_que_no_es_foto_se_rechaza()
    {
        using var operador = await api.OperadorAsync();
        var (cobranzaId, pagoId, _) = await PagoAsync(operador);

        await (await AdjuntarAsync(operador, cobranzaId, pagoId, "%PDF-1.4 comprobante"u8.ToArray()))
            .ProblemaAsync(400, "archivo_no_es_imagen");
        (await operador.PostAsJsonAsync($"/api/cobranzas/{cobranzaId}/pagos/{pagoId}/anular", new { motivo = "Se cargó dos veces" }))
            .EnsureSuccessStatusCode();
        await (await AdjuntarAsync(operador, cobranzaId, pagoId)).ProblemaAsync(400, "pago_anulado");
        Assert.Null(await ComprobanteDe(operador, cobranzaId, pagoId));
    }

    [Fact]
    public async Task El_pago_de_otra_cobranza_responde_404()
    {
        using var operador = await api.OperadorAsync();
        var (cobranzaId, _, _) = await PagoAsync(operador);
        var (_, pagoDeOtra, _) = await PagoAsync(operador);

        await (await AdjuntarAsync(operador, cobranzaId, pagoDeOtra)).ProblemaAsync(404, "no_encontrado");
    }
}
