using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

/// <summary>La HP para imprimir o guardar en PDF (6.5, 7.3.26), de la secretaria.</summary>
[Collection(ColeccionApi.Nombre)]
public class HojaParaImprimirTests(ApiDePrueba api)
{
    /// <summary>Una HP confirmada con dos líneas: 4 del primer producto (Bs 38) con Bs 1 de descuento y 6 del segundo (Bs 16,50).</summary>
    private static async Task<(int HojaId, int Primero, int Segundo)> HojaAsync(HttpClient operador, bool credito)
    {
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var primero = await operador.CrearProductoAsync(38m);
        var segundo = await operador.CrearProductoAsync(16.50m);
        var respuesta = await operador.PostAsJsonAsync("/api/hojas-pedido", new
        {
            clienteId,
            latitud = -17.7833m,
            longitud = -63.1821m,
            tipoVenta = credito ? 1 : 0,
            descuento = 7m,
            detalles = new object[]
            {
                new { productoId = primero, cantidad = 4, descuento = 1m },
                new { productoId = segundo, cantidad = 6 }
            }
        });
        respuesta.EnsureSuccessStatusCode();
        var hojaId = (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt32();
        (await operador.PostAsync($"/api/hojas-pedido/{hojaId}/confirmar", null)).EnsureSuccessStatusCode();
        return (hojaId, primero, segundo);
    }

    private async Task<HttpClient> SecretariaAsync() => api.ClienteCon((await api.CrearUsuarioAsync(Roles.Secretaria)).Token);

    [Fact]
    public async Task Trae_la_HP_con_su_cliente_sus_lineas_en_orden_y_su_cobranza()
    {
        using var operador = await api.OperadorAsync();
        var (hojaId, primero, segundo) = await HojaAsync(operador, credito: true);
        using var secretaria = await SecretariaAsync();

        var hoja = await (await secretaria.GetAsync($"/api/hojas-pedido/{hojaId}/impresion")).LeerJsonAsync();

        Assert.StartsWith("HP-", hoja.GetProperty("numero").GetString());
        var cliente = hoja.GetProperty("cliente");
        Assert.Equal("Calle de prueba", cliente.GetProperty("direccion").GetString());
        Assert.StartsWith("CLI-", cliente.GetProperty("codigo").GetString());
        Assert.False(string.IsNullOrEmpty(cliente.GetProperty("celular").GetString()));
        Assert.Equal([primero, segundo], hoja.GetProperty("detalles").EnumerateArray().Select(d => d.GetProperty("productoId").GetInt32()));
        // 4 × (38 − 1) + 6 × 16,50 − 7 = 148 + 99 − 7, la HP-2026-00018 del 8.6.5.
        Assert.Equal((247m, 7m, 240m), (hoja.GetProperty("subTotal").GetDecimal(), hoja.GetProperty("descuento").GetDecimal(),
            hoja.GetProperty("total").GetDecimal()));
        Assert.StartsWith("COB-", hoja.GetProperty("cobranza").GetProperty("numero").GetString());
        Assert.Equal(JsonValueKind.Null, hoja.GetProperty("fechaAnulacion").ValueKind);
    }

    [Fact]
    public async Task La_anulada_trae_la_fecha_quien_la_anulo_y_el_motivo()
    {
        using var operador = await api.OperadorAsync();
        var (hojaId, _, _) = await HojaAsync(operador, credito: false);
        (await operador.PostAsJsonAsync($"/api/hojas-pedido/{hojaId}/anular", new { motivo = "Venta duplicada" })).EnsureSuccessStatusCode();
        using var secretaria = await SecretariaAsync();

        var hoja = await (await secretaria.GetAsync($"/api/hojas-pedido/{hojaId}/impresion")).LeerJsonAsync();

        Assert.Equal(2, hoja.GetProperty("estadoHP").GetInt32());
        Assert.Equal("Venta duplicada", hoja.GetProperty("motivoAnulacion").GetString());
        Assert.False(string.IsNullOrEmpty(hoja.GetProperty("anuladoPor").GetProperty("nombreCompleto").GetString()));
        Assert.NotEqual(JsonValueKind.Null, hoja.GetProperty("fechaAnulacion").ValueKind);
        Assert.Equal(JsonValueKind.Null, hoja.GetProperty("cobranza").ValueKind);
    }

    [Fact]
    public async Task Una_HP_ajena_responde_404_y_sin_el_permiso_403()
    {
        using var operador = await api.OperadorAsync();
        var (hojaId, _, _) = await HojaAsync(operador, credito: false);
        using var admin = await api.AdminAsync();
        var (_, rol) = await admin.CrearRolAsync(Permisos.Reportes.ImprimirHp);
        using var sinCartera = api.ClienteCon((await api.CrearUsuarioAsync(rol)).Token);

        await (await sinCartera.GetAsync($"/api/hojas-pedido/{hojaId}/impresion")).ProblemaAsync(404);

        using var gerente = api.ClienteCon((await api.CrearUsuarioAsync(Roles.GerenteGeneral)).Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await gerente.GetAsync($"/api/hojas-pedido/{hojaId}/impresion")).StatusCode);
    }
}
