using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ControlGestion.API.Tests.Infraestructura;

public record UsuarioDePrueba(string Id, string Correo, string Contrasena, string Token);

/// <summary>
/// Atajos para hablar con la API y para crear los datos que necesita cada prueba. Cada dato lleva un nombre
/// único, así ninguna prueba depende de lo que creó otra.
/// </summary>
public static class Ayudantes
{
    public static string Unico(string prefijo) => $"{prefijo}-{Guid.NewGuid().ToString("N")[..8]}";

    public static async Task<string> IniciarSesionAsync(this ApiDePrueba api, string correo, string contrasena)
    {
        using var cliente = api.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new { email = correo, password = contrasena });
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.LeerJsonAsync()).GetProperty("token").GetString()!;
    }

    public static HttpClient ClienteCon(this ApiDePrueba api, string token)
    {
        var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    public static async Task<HttpClient> AdminAsync(this ApiDePrueba api) =>
        api.ClienteCon(await api.IniciarSesionAsync(ApiDePrueba.CorreoAdmin, api.ContrasenaAdmin));

    /// <summary>Un usuario con todos los permisos, para preparar datos de negocio (zonas, clientes, HP…).</summary>
    public static async Task<HttpClient> OperadorAsync(this ApiDePrueba api) =>
        api.ClienteCon(await api.IniciarSesionAsync(ApiDePrueba.CorreoOperador, api.ContrasenaOperador));

    public static async Task<UsuarioDePrueba> CrearUsuarioAsync(this ApiDePrueba api, string rol)
    {
        var correo = $"{Unico("t").ToLowerInvariant()}@test.local";
        var contrasena = $"Clave{Guid.NewGuid():N}1";
        using var admin = await api.AdminAsync();
        var respuesta = await admin.PostAsJsonAsync("/api/usuarios",
            new { email = correo, nombreCompleto = $"Prueba {rol}", password = contrasena, rol });
        respuesta.EnsureSuccessStatusCode();
        var id = (await respuesta.LeerJsonAsync()).GetProperty("id").GetString()!;
        return new UsuarioDePrueba(id, correo, contrasena, await api.IniciarSesionAsync(correo, contrasena));
    }

    /// <summary>Crea un rol con esos permisos y devuelve su id y su nombre.</summary>
    public static async Task<(string Id, string Nombre)> CrearRolAsync(this HttpClient admin, params string[] permisos)
    {
        var nombre = Unico("Rol");
        var respuesta = await admin.PostAsJsonAsync("/api/roles", new { nombre, permisos });
        respuesta.EnsureSuccessStatusCode();
        return ((await respuesta.LeerJsonAsync()).GetProperty("id").GetString()!, nombre);
    }

    public static async Task<JsonElement> LeerJsonAsync(this HttpResponseMessage respuesta) =>
        await respuesta.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Comprueba el código HTTP y que el cuerpo sea un ProblemDetails completo.</summary>
    public static async Task<JsonElement> ProblemaAsync(this HttpResponseMessage respuesta, int estado, string? codigo = null)
    {
        Assert.Equal(estado, (int)respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.LeerJsonAsync();
        foreach (var campo in new[] { "title", "detail", "codigo", "traceId" })
            Assert.True(problema.TryGetProperty(campo, out _), $"El ProblemDetails no trae '{campo}'.");
        if (codigo != null)
            Assert.Equal(codigo, problema.GetProperty("codigo").GetString());
        return problema;
    }

    public static async Task<int> CrearZonaAsync(this HttpClient admin, string? promotorId = null)
    {
        var codigo = Unico("Z");
        var respuesta = await admin.PostAsJsonAsync("/api/zonas",
            new { nombre = $"Zona {codigo}", codigo, promotorId });
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt32();
    }

    public static async Task<int> CrearProductoAsync(this HttpClient admin, decimal precio, decimal stockInicial = 100)
    {
        var sku = Unico("P");
        var respuesta = await admin.PostAsJsonAsync("/api/productos",
            new { sku, descripcion = $"Producto {sku}", precio, categoria = "Pruebas" });
        respuesta.EnsureSuccessStatusCode();
        var id = (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt32();
        if (stockInicial != 0)
            (await admin.PostAsJsonAsync($"/api/inventario/{id}/ajuste",
                new { cantidad = stockInicial, observacion = "Stock inicial de la prueba" })).EnsureSuccessStatusCode();
        return id;
    }

    public static async Task<int> CrearClienteAsync(this HttpClient admin, int zonaId, string? promotorId = null,
        decimal limiteCredito = 1000)
    {
        var respuesta = await admin.PostAsJsonAsync("/api/clientes", new
        {
            nombreCompleto = $"Cliente {Unico("C")}",
            telefono = "70000000",
            direccionTexto = "Calle de prueba",
            latitud = -17.7833m,
            longitud = -63.1821m,
            zonaId,
            promotorId,
            tieneCredito = limiteCredito > 0,
            limiteCredito
        });
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt32();
    }

    public static async Task<int> CrearHojaPedidoAsync(this HttpClient vendedor, int clienteId, int zonaId, int productoId,
        decimal cantidad, bool credito = true)
    {
        var respuesta = await vendedor.PostAsJsonAsync("/api/hojas-pedido", new
        {
            clienteId,
            zonaId,
            latitud = -17.7833m,
            longitud = -63.1821m,
            tipoVenta = credito ? 1 : 0,
            detalles = new[] { new { productoId, cantidad } }
        });
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt32();
    }

    /// <summary>Crea y confirma una HP a crédito, y devuelve el id de la cobranza que se generó.</summary>
    public static async Task<int> CrearCobranzaAsync(this HttpClient vendedor, int clienteId, int zonaId, int productoId,
        decimal cantidad)
    {
        var hojaId = await vendedor.CrearHojaPedidoAsync(clienteId, zonaId, productoId, cantidad);
        (await vendedor.PostAsync($"/api/hojas-pedido/{hojaId}/confirmar", null)).EnsureSuccessStatusCode();

        var cobranzas = await (await vendedor.GetAsync($"/api/cobranzas?clienteId={clienteId}")).LeerJsonAsync();
        return cobranzas.EnumerateArray()
            .Single(c => c.GetProperty("hojaPedido").GetProperty("id").GetInt32() == hojaId)
            .GetProperty("id").GetInt32();
    }

    /// <summary>Lanza todas las peticiones a la vez: ninguna sale hasta que todas están listas.</summary>
    public static async Task<HttpResponseMessage[]> EnviarALaVezAsync(int cantidad, Func<int, Task<HttpResponseMessage>> peticion)
    {
        var largada = new TaskCompletionSource();
        var tareas = Enumerable.Range(0, cantidad).Select(async i =>
        {
            await largada.Task;
            return await peticion(i);
        }).ToArray();
        largada.SetResult();
        return await Task.WhenAll(tareas);
    }
}
