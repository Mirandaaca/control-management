using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlGestion.API.Data;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Tests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

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
    public async Task Cambiar_la_ubicacion_de_un_cliente_pide_capturar_la_ubicacion()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        using var operador = await api.OperadorAsync();
        using var admin = await api.AdminAsync();
        var zonaId = await operador.CrearZonaAsync(promotor.Id);
        var id = await operador.CrearClienteAsync(zonaId);
        object Datos(decimal latitud) => new
        {
            nombreCompleto = "Cliente con ubicación", telefono = "77712345", direccionTexto = "Calle 1",
            latitud, longitud = -63.1821m, zonaId
        };

        // La secretaria corrige los datos, pero la ubicación la actualiza el promotor (7.3.6, ext. 3.1b; 7.3.8, paso 3).
        using var comoSecretaria = api.ClienteCon(secretaria.Token);
        var problema = await (await comoSecretaria.PutAsJsonAsync($"/api/clientes/{id}", Datos(-17.7900m))).ProblemaAsync(403, "sin_permiso");
        Assert.Equal("No tienes permiso para cambiar la ubicación del cliente.", problema.GetProperty("detail").GetString());
        (await comoSecretaria.PutAsJsonAsync($"/api/clientes/{id}", Datos(-17.7833m))).EnsureSuccessStatusCode();

        using var comoPromotor = api.ClienteCon(promotor.Token);
        (await comoPromotor.PutAsJsonAsync($"/api/clientes/{id}", Datos(-17.7900m))).EnsureSuccessStatusCode();
        Assert.Equal(-17.79m, (await (await operador.GetAsync($"/api/clientes/{id}")).LeerJsonAsync()).GetProperty("latitud").GetDecimal());

        // Lo que cuenta es el permiso, no el rol.
        var (_, rol) = await admin.CrearRolAsync(Permisos.Ventas.EditarCliente, Permisos.Ventas.CapturarUbicacion,
            Permisos.Seguridad.VerCarteraCompleta);
        using var conPermiso = api.ClienteCon((await api.CrearUsuarioAsync(rol)).Token);
        (await conPermiso.PutAsJsonAsync($"/api/clientes/{id}", Datos(-17.7950m))).EnsureSuccessStatusCode();
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
            nombre = zona.GetProperty("nombre").GetString(),
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
    public async Task Las_opciones_traen_la_direccion_del_cliente_para_proponerla_en_un_pedido()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());

        var opcion = (await (await operador.GetAsync("/api/clientes/opciones")).LeerJsonAsync()).EnumerateArray()
            .Single(c => c.GetProperty("id").GetInt32() == clienteId);

        Assert.Equal("Calle de prueba", opcion.GetProperty("direccionTexto").GetString());
        Assert.False(opcion.GetProperty("inactivo").GetBoolean());
        Assert.True(opcion.GetProperty("estado").GetProperty("delSistema").GetBoolean());
    }

    [Fact]
    public async Task La_lista_trae_la_direccion_de_cada_cliente()
    {
        using var operador = await api.OperadorAsync();
        var clienteId = await operador.CrearClienteAsync(await operador.CrearZonaAsync());
        var codigo = (await (await operador.GetAsync($"/api/clientes/{clienteId}")).LeerJsonAsync()).GetProperty("codigo").GetString();

        var item = (await (await operador.GetAsync($"/api/clientes?buscar={codigo}")).LeerJsonAsync())
            .GetProperty("items").EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == clienteId);

        Assert.Equal("Calle de prueba", item.GetProperty("direccionTexto").GetString());
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

        Assert.Equal("Disponible para re-visita", creado.GetProperty("estado").GetProperty("nombre").GetString());
        Assert.Equal(JsonValueKind.Null, creado.GetProperty("proximaVisita").ValueKind);
        Assert.Contains(id, await IdsAsync(operador, $"?zonaId={zonaId}&porVisitar=true"));
    }

    private static string EstadoDe(JsonElement cliente) => cliente.GetProperty("estado").GetProperty("nombre").GetString()!;

    [Fact]
    public async Task Editar_no_cambia_el_estado_del_cliente()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var id = await operador.CrearClienteAsync(zonaId);

        var editado = await (await operador.PutAsJsonAsync($"/api/clientes/{id}", Cliente(zonaId))).LeerJsonAsync();

        Assert.Equal("Disponible para re-visita", EstadoDe(editado));
    }

    [Fact]
    public async Task La_secretaria_desactiva_un_cliente_con_un_estado_y_lo_devuelve_al_ciclo()
    {
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var id = await operador.CrearClienteAsync(zonaId);
        using var cliente = api.ClienteCon(secretaria.Token);
        var enOpciones = async () => (await (await cliente.GetAsync("/api/clientes/opciones")).LeerJsonAsync())
            .EnumerateArray().Any(c => c.GetProperty("id").GetInt32() == id);

        var inactivo = await (await cliente.PutAsJsonAsync($"/api/clientes/{id}/estado", new { estadoClienteId = 4 })).LeerJsonAsync();

        Assert.Equal("Inactivo", EstadoDe(inactivo));
        Assert.DoesNotContain(id, await IdsAsync(cliente, $"?zonaId={zonaId}&porVisitar=true"));
        Assert.False(await enOpciones());
        // La app los pide igual, para mostrarlos deshabilitados con su estado (7.3.5, ext. 2.2c).
        var conInactivos = (await (await cliente.GetAsync("/api/clientes/opciones?incluirInactivos=true")).LeerJsonAsync())
            .EnumerateArray().Single(c => c.GetProperty("id").GetInt32() == id);
        Assert.True(conInactivos.GetProperty("inactivo").GetBoolean());
        Assert.Equal("Inactivo", conInactivos.GetProperty("estado").GetProperty("nombre").GetString());

        var deVuelta = await (await cliente.PutAsJsonAsync($"/api/clientes/{id}/estado", new { estadoClienteId = (int?)null }))
            .LeerJsonAsync();

        Assert.Equal("Disponible para re-visita", EstadoDe(deVuelta));
        Assert.Contains(id, await IdsAsync(cliente, $"?zonaId={zonaId}&porVisitar=true"));
        Assert.True(await enOpciones());
    }

    [Fact]
    public async Task Los_estados_del_sistema_no_se_asignan_a_mano_y_el_promotor_no_asigna_estados()
    {
        var secretaria = await api.CrearUsuarioAsync(Roles.Secretaria);
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var id = await operador.CrearClienteAsync(await operador.CrearZonaAsync(promotor.Id));

        await (await api.ClienteCon(secretaria.Token).PutAsJsonAsync($"/api/clientes/{id}/estado", new { estadoClienteId = 1 }))
            .ProblemaAsync(400, "estado_del_sistema");
        await (await api.ClienteCon(promotor.Token).PutAsJsonAsync($"/api/clientes/{id}/estado", new { estadoClienteId = 4 }))
            .ProblemaAsync(403, "sin_permiso");
    }

    [Fact]
    public async Task Los_clientes_no_se_borran()
    {
        using var operador = await api.OperadorAsync();
        var id = await operador.CrearClienteAsync(await operador.CrearZonaAsync());

        await (await operador.DeleteAsync($"/api/clientes/{id}")).ProblemaAsync(405, "metodo_no_permitido");
    }

    [Fact]
    public async Task Un_celular_repetido_se_avisa_y_se_registra_si_se_confirma()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var telefono = $"6{Random.Shared.Next(1000000, 9999999)}";
        var primero = await (await operador.PostAsJsonAsync("/api/clientes", Cliente(zonaId, telefono))).LeerJsonAsync();

        var problema = await (await operador.PostAsJsonAsync("/api/clientes", Cliente(zonaId, telefono)))
            .ProblemaAsync(409, "telefono_repetido");
        Assert.Contains(primero.GetProperty("codigo").GetString()!, problema.GetProperty("detail").GetString());

        var confirmado = await operador.PostAsJsonAsync("/api/clientes", new
        {
            nombreCompleto = "Mismo celular", telefono, direccionTexto = "Calle", latitud = -17.76m, longitud = -63.18m,
            zonaId, confirmarTelefonoRepetido = true
        });
        Assert.Equal(HttpStatusCode.Created, confirmado.StatusCode);
    }

    [Fact]
    public async Task El_aviso_de_celular_no_muestra_un_cliente_de_otra_cartera()
    {
        var promotor = await api.CrearUsuarioAsync(Roles.Promotor);
        var otro = await api.CrearUsuarioAsync(Roles.Promotor);
        using var operador = await api.OperadorAsync();
        var telefono = $"6{Random.Shared.Next(1000000, 9999999)}";
        (await operador.PostAsJsonAsync("/api/clientes", Cliente(await operador.CrearZonaAsync(otro.Id), telefono))).EnsureSuccessStatusCode();
        var suya = await operador.CrearZonaAsync(promotor.Id);

        var problema = await (await api.ClienteCon(promotor.Token).PostAsJsonAsync("/api/clientes", Cliente(suya, telefono)))
            .ProblemaAsync(409, "telefono_repetido");

        Assert.Equal("Ya existe otro cliente con ese celular.", problema.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Una_venta_es_una_visita_y_la_proxima_usa_los_dias_de_re_visita_generales()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var productoId = await operador.CrearProductoAsync(10m);
        var clienteId = await operador.CrearClienteAsync(zonaId);
        (await operador.PutAsJsonAsync("/api/configuraciones/dias-revisita", new { dias = 7 })).EnsureSuccessStatusCode();
        try
        {
            // HP a crédito para un cliente sin límite de crédito: la validación credito_insuficiente ya no existe.
            await operador.CrearCobranzaAsync(clienteId, productoId, 1);

            var cliente = await (await operador.GetAsync($"/api/clientes/{clienteId}")).LeerJsonAsync();
            var faltan = cliente.GetProperty("proximaVisita").GetDateTime() - DateTime.UtcNow;
            Assert.InRange(faltan.TotalDays, 6.9, 7.1);
            Assert.Equal("Activo", EstadoDe(cliente));
        }
        finally
        {
            // Vuelve al valor sembrado: las demás pruebas cuentan con él.
            await operador.PutAsJsonAsync("/api/configuraciones/dias-revisita", new { dias = 15 });
        }
    }

    [Fact]
    public async Task Los_estados_dicen_cuales_asigna_el_sistema()
    {
        using var operador = await api.OperadorAsync();
        var estados = (await (await operador.GetAsync("/api/clientes/estados")).LeerJsonAsync()).EnumerateArray().ToList();

        Assert.Equal([1, 2], estados.Where(e => e.GetProperty("delSistema").GetBoolean()).Select(e => e.GetProperty("id").GetInt32()));
        Assert.Contains(estados, e => e.GetProperty("nombre").GetString() == "Inactivo" && !e.GetProperty("delSistema").GetBoolean());
    }

    [Fact]
    public void Los_estados_sembrados_tienen_el_rojo_el_verde_y_un_color_de_la_paleta()
    {
        using var scope = api.Services.CreateScope();
        var colores = scope.ServiceProvider.GetRequiredService<AppDbContext>().EstadosCliente.ToDictionary(e => e.Id, e => e.Color);

        Assert.Equal(PaletaDeEstados.Activo, colores[EstadosDeCliente.Activo]);
        Assert.Equal(PaletaDeEstados.Disponible, colores[EstadosDeCliente.DisponibleParaRevisita]);
        Assert.Equal(PaletaDeEstados.Pizarra, colores[EstadosDeCliente.Inactivo]);
    }

    [Fact]
    public async Task Las_opciones_aceptan_un_limite_y_vienen_ordenadas_por_nombre()
    {
        using var operador = await api.OperadorAsync();
        var zonaId = await operador.CrearZonaAsync();
        var prefijo = Ayudantes.Unico("PAR");
        foreach (var letra in new[] { "C", "A", "B" })
            (await operador.PostAsJsonAsync("/api/clientes", new
            {
                nombreCompleto = $"{prefijo} {letra}", telefono = $"7{Random.Shared.Next(1000000, 9999999)}",
                direccionTexto = "Calle", latitud = -17.76m, longitud = -63.18m, zonaId
            })).EnsureSuccessStatusCode();

        async Task<List<string>> NombresAsync(string consulta) =>
            (await (await operador.GetAsync($"/api/clientes/opciones?buscar={prefijo}{consulta}")).LeerJsonAsync())
                .EnumerateArray().Select(c => c.GetProperty("nombreCompleto").GetString()!).ToList();

        Assert.Equal([$"{prefijo} A", $"{prefijo} B"], await NombresAsync("&limite=2"));
        Assert.Equal(3, (await NombresAsync("")).Count);
        var problema = await (await operador.GetAsync("/api/clientes/opciones?limite=0")).ProblemaAsync(400, "validacion");
        Assert.True(problema.GetProperty("errors").TryGetProperty("limite", out _));
    }

    [Fact]
    public async Task El_estado_Con_Deuda_Vencida_ya_no_existe_ni_se_asigna()
    {
        using var operador = await api.OperadorAsync();
        var id = await operador.CrearClienteAsync(await operador.CrearZonaAsync());

        var estados = (await (await operador.GetAsync("/api/clientes/estados")).LeerJsonAsync()).EnumerateArray();
        Assert.DoesNotContain(estados, e => e.GetProperty("id").GetInt32() == 3 || e.GetProperty("nombre").GetString() == "Con Deuda Vencida");
        await (await operador.PutAsJsonAsync($"/api/clientes/{id}/estado", new { estadoClienteId = 3 })).ProblemaAsync(400, "estado_no_existe");
    }
}
