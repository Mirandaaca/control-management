using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Seguridad;
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
        (await (await cliente.GetAsync($"/api/clientes{consulta}")).LeerJsonAsync()).GetProperty("items").EnumerateArray().ToList();

    private static async Task<List<int>> IdsAsync(HttpClient cliente, string consulta = "") =>
        (await ListarAsync(cliente, consulta)).Select(c => c.GetProperty("id").GetInt32()).ToList();

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
        Assert.Equal(zonaId, leido.GetProperty("zona").GetProperty("id").GetInt32());
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
    public async Task Al_cambiar_el_promotor_de_una_zona_sus_clientes_pasan_al_nuevo()
    {
        var anterior = await api.CrearUsuarioAsync(Roles.Promotor);
        var nuevo = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync(anterior.Id);
        var clienteId = await operador.CrearClienteAsync(zonaId);
        var zona = await (await operador.GetAsync($"/api/zonas/{zonaId}")).LeerJsonAsync();

        (await operador.PutAsJsonAsync($"/api/zonas/{zonaId}", new
        {
            id = zonaId,
            nombre = zona.GetProperty("nombre").GetString(),
            codigo = zona.GetProperty("codigo").GetString(),
            promotorId = nuevo.Id
        })).EnsureSuccessStatusCode();

        var cliente = await (await operador.GetAsync($"/api/clientes/{clienteId}")).LeerJsonAsync();
        Assert.Equal(nuevo.Id, cliente.GetProperty("promotor").GetProperty("id").GetString());
    }

    [Fact]
    public async Task La_lista_filtra_por_zona()
    {
        using var operador = await api.OperadorAsync();
        var zonaA = await operador.CrearZonaAsync();
        var zonaB = await operador.CrearZonaAsync();
        var deA = await operador.CrearClienteAsync(zonaA);
        var deB = await operador.CrearClienteAsync(zonaB);

        var ids = await IdsAsync(operador, $"?zonaId={zonaA}");

        Assert.Contains(deA, ids);
        Assert.DoesNotContain(deB, ids);
    }

    [Fact]
    public async Task Un_promotor_ve_solo_los_clientes_de_su_cartera()
    {
        var promotorA = await api.CrearUsuarioAsync(Roles.Promotor);
        var promotorB = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var zonaA = await operador.CrearZonaAsync(promotorA.Id);
        var zonaB = await operador.CrearZonaAsync(promotorB.Id);
        var deA = await operador.CrearClienteAsync(zonaA);
        var deB = await operador.CrearClienteAsync(zonaB);
        using var clienteA = api.ClienteCon(promotorA.Token);

        var lista = await IdsAsync(clienteA, "?tamano=100");
        var opciones = (await (await clienteA.GetAsync("/api/clientes/opciones")).LeerJsonAsync())
            .EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).ToList();

        Assert.Contains(deA, lista);
        Assert.DoesNotContain(deB, lista);
        Assert.Contains(deA, opciones);
        Assert.DoesNotContain(deB, opciones);
        await (await clienteA.GetAsync($"/api/clientes/{deB}")).ProblemaAsync(404, "no_encontrado");
        await (await clienteA.PutAsJsonAsync($"/api/clientes/{deB}", Cliente(zonaB))).ProblemaAsync(404, "no_encontrado");
        Assert.Contains(deB, await IdsAsync(operador, $"?zonaId={zonaB}"));
    }

    [Fact]
    public async Task La_busqueda_no_distingue_tildes_ni_mayusculas_y_tambien_busca_por_codigo()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var marca = Ayudantes.Unico("Ñ");
        var respuesta = await operador.PostAsJsonAsync("/api/clientes", new
        {
            nombreCompleto = $"María Ángela {marca}", telefono = "70011223", direccionTexto = "Plan 3000",
            latitud = -17.80m, longitud = -63.15m, zonaId
        });
        var creado = await respuesta.LeerJsonAsync();
        var id = creado.GetProperty("id").GetInt32();
        var codigo = creado.GetProperty("codigo").GetString();

        Assert.Contains(id, await IdsAsync(operador, $"?buscar=maria angela {marca.ToLowerInvariant()}"));
        Assert.Contains(id, await IdsAsync(operador, $"?buscar={codigo}"));
        Assert.DoesNotContain(id, await IdsAsync(operador, $"?buscar=no-existe-{marca}"));
    }

    [Fact]
    public async Task La_lista_viene_paginada_con_el_total()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        for (var i = 0; i < 3; i++) await operador.CrearClienteAsync(zonaId);

        var pagina = await (await operador.GetAsync($"/api/clientes?zonaId={zonaId}&tamano=2&pagina=2")).LeerJsonAsync();

        Assert.Equal(3, pagina.GetProperty("total").GetInt32());
        Assert.Equal(2, pagina.GetProperty("totalPaginas").GetInt32());
        Assert.Single(pagina.GetProperty("items").EnumerateArray());
        await (await operador.GetAsync("/api/clientes?tamano=500")).ProblemaAsync(400, "validacion");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0.0, 0.0)]
    [InlineData(200.0, -63.18)]
    public async Task Sin_una_ubicacion_valida_no_se_guarda_el_cliente(double? latitud, double? longitud)
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();

        var problema = await (await operador.PostAsJsonAsync("/api/clientes", new
        {
            nombreCompleto = "Sin ubicación", telefono = "70000001", direccionTexto = "Calle", latitud, longitud, zonaId
        })).ProblemaAsync(400, "validacion");

        Assert.True(problema.GetProperty("errors").TryGetProperty("latitud", out _));
    }

    private static string? PromotorDe(JsonElement cliente) =>
        cliente.GetProperty("promotor").ValueKind == JsonValueKind.Null ? null : cliente.GetProperty("promotor").GetProperty("id").GetString();

    [Fact]
    public async Task El_cliente_toma_el_promotor_de_su_zona_y_lo_cambia_con_ella()
    {
        var promotorA = await api.CrearUsuarioAsync(Roles.Promotor);
        var promotorB = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var zonaA = await operador.CrearZonaAsync(promotorA.Id);
        var zonaB = await operador.CrearZonaAsync(promotorB.Id);

        var creado = await (await operador.PostAsJsonAsync("/api/clientes", Cliente(zonaA))).LeerJsonAsync();
        var movido = await (await operador.PutAsJsonAsync($"/api/clientes/{creado.GetProperty("id").GetInt32()}", Cliente(zonaB)))
            .LeerJsonAsync();

        Assert.Equal(promotorA.Id, PromotorDe(creado));
        Assert.Equal(promotorB.Id, PromotorDe(movido));
    }

    [Fact]
    public async Task Un_promotor_registra_clientes_solo_en_sus_zonas()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var otro = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var suya = await operador.CrearZonaAsync(promotor.Id);
        var ajena = await operador.CrearZonaAsync(otro.Id);
        using var cliente = api.ClienteCon(promotor.Token);

        var respuesta = await cliente.PostAsJsonAsync("/api/clientes", Cliente(suya));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Equal(promotor.Id, PromotorDe(await respuesta.LeerJsonAsync()));
        await (await cliente.PostAsJsonAsync("/api/clientes", Cliente(ajena))).ProblemaAsync(400, "zona_fuera_de_cartera");
    }

    [Fact]
    public async Task Un_cliente_nuevo_esta_disponible_para_re_visita_y_sale_por_visitar()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var creado = await (await operador.PostAsJsonAsync("/api/clientes", Cliente(zonaId))).LeerJsonAsync();
        var id = creado.GetProperty("id").GetInt32();

        Assert.Equal("Disponible para Re-visita", creado.GetProperty("estado").GetProperty("nombre").GetString());
        Assert.Equal(JsonValueKind.Null, creado.GetProperty("proximaVisita").ValueKind);
        Assert.Contains(id, await IdsAsync(operador, $"?zonaId={zonaId}&porVisitar=true"));
    }
}
