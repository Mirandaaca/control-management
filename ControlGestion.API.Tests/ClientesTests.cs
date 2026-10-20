using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Tests.Infraestructura;

namespace ControlGestion.API.Tests;

[Collection(ColeccionApi.Nombre)]
public class ClientesTests(ApiDePrueba api)
{
    private static object Cliente(int zonaId, string? telefono = null) => new
    {
        nombreCompleto = $"Cliente {Ayudantes.Unico("C")}",
        telefono = telefono ?? $"7{Random.Shared.Next(1000000, 9999999)}",
        direccionTexto = "Av. Banzer, 4to anillo",
        latitud = -17.7600m,
        longitud = -63.1800m,
        zonaId
    };

    private static async Task<List<JsonElement>> ListarAsync(HttpClient cliente, string consulta = "") =>
        (await (await cliente.GetAsync($"/api/clientes{consulta}")).LeerJsonAsync()).EnumerateArray().ToList();

    [Fact]
    public async Task Registrar_un_cliente_le_da_su_codigo_y_se_puede_consultar()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();

        var respuesta = await operador.PostAsJsonAsync("/api/clientes", Cliente(zonaId, "71234567"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creado = await respuesta.LeerJsonAsync();
        Assert.StartsWith("CLI-", creado.GetProperty("codigo").GetString());
        var id = creado.GetProperty("id").GetInt32();
        var leido = await (await operador.GetAsync($"/api/clientes/{id}")).LeerJsonAsync();
        Assert.Equal("71234567", leido.GetProperty("telefono").GetString());
        Assert.Equal(zonaId, leido.GetProperty("zonaId").GetInt32());
    }

    [Fact]
    public async Task Editar_cambia_los_datos_del_cliente()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var id = await operador.CrearClienteAsync(zonaId);

        var respuesta = await operador.PutAsJsonAsync($"/api/clientes/{id}", new
        {
            nombreCompleto = "Nombre corregido",
            telefono = "76543210",
            direccionTexto = "Calle nueva",
            latitud = -17.7700m,
            longitud = -63.1900m,
            zonaId
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var leido = await (await operador.GetAsync($"/api/clientes/{id}")).LeerJsonAsync();
        Assert.Equal("Nombre corregido", leido.GetProperty("nombreCompleto").GetString());
        Assert.Equal("Calle nueva", leido.GetProperty("direccionTexto").GetString());
    }

    [Fact]
    public async Task La_lista_filtra_por_zona()
    {
        using var operador = await api.OperadorAsync();
        var zonaA = await operador.CrearZonaAsync();
        var zonaB = await operador.CrearZonaAsync();
        var deA = await operador.CrearClienteAsync(zonaA);
        var deB = await operador.CrearClienteAsync(zonaB);

        var ids = (await ListarAsync(operador, $"?zonaId={zonaA}")).Select(c => c.GetProperty("id").GetInt32()).ToList();

        Assert.Contains(deA, ids);
        Assert.DoesNotContain(deB, ids);
    }
}
