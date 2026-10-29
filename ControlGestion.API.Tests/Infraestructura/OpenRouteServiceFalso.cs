using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ControlGestion.API.Tests.Infraestructura;

/// <summary>
/// Responde como OpenRouteService (POST /v2/directions/{perfil}/json), sin salir a internet. Guarda cada pedido (la URL,
/// la clave y el cuerpo) para que la prueba revise qué mandó la API. Sin <see cref="Responder"/>, responde un camino
/// principal y una alternativa, con la línea codificada como la manda el proveedor.
/// </summary>
public class OpenRouteServiceFalso : HttpMessageHandler
{
    public record Pedido(Uri Url, string? Clave, JsonElement Cuerpo);

    /// <summary>La línea del camino principal: sale del origen de las pruebas y llega al cliente (-17.7833, -63.1821).</summary>
    public static readonly (decimal Latitud, decimal Longitud)[] Principal =
        [(-17.78m, -63.19m), (-17.78m, -63.185m), (-17.7833m, -63.185m), (-17.7833m, -63.1821m)];

    public static readonly (decimal Latitud, decimal Longitud)[] Alternativa =
        [(-17.78m, -63.19m), (-17.7833m, -63.19m), (-17.7833m, -63.1821m)];

    public ConcurrentQueue<Pedido> Pedidos { get; } = new();

    /// <summary>Otra respuesta, o una excepción para simular la red caída o el tiempo agotado.</summary>
    public Func<HttpResponseMessage>? Responder { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken cancelacion)
    {
        var cuerpo = JsonDocument.Parse(await pedido.Content!.ReadAsStringAsync(cancelacion)).RootElement.Clone();
        var clave = pedido.Headers.TryGetValues("Authorization", out var valores) ? valores.Single() : null;
        Pedidos.Enqueue(new Pedido(pedido.RequestUri!, clave, cuerpo));
        return Responder?.Invoke() ?? Json(HttpStatusCode.OK, Ejemplo);
    }

    public static HttpResponseMessage Json(HttpStatusCode estado, object cuerpo) =>
        new(estado) { Content = new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json") };

    // Los nombres van como los manda el proveedor (way_points, exit_number). Una calle sin nombre llega como "-".
    private static readonly object Ejemplo = new
    {
        routes = new object[]
        {
            new
            {
                summary = new { distance = 1202.4, duration = 145.6 },
                geometry = Codificar(Principal),
                segments = new[]
                {
                    new
                    {
                        distance = 1202.4,
                        duration = 145.6,
                        steps = new object[]
                        {
                            new { distance = 530.2, duration = 60.0, type = 11, name = "Calle Sucre", way_points = new[] { 0, 1 } },
                            new { distance = 365.0, duration = 45.4, type = 1, name = "Calle 4", way_points = new[] { 1, 2 } },
                            new { distance = 307.2, duration = 40.2, type = 7, name = "Av. Banzer", way_points = new[] { 2, 3 }, exit_number = 2 },
                            new { distance = 0.0, duration = 0.0, type = 10, name = "-", way_points = new[] { 3, 3 } }
                        }
                    }
                }
            },
            new
            {
                summary = new { distance = 1400.0, duration = 190.0 },
                geometry = Codificar(Alternativa),
                segments = new[]
                {
                    new
                    {
                        distance = 1400.0,
                        duration = 190.0,
                        steps = new object[]
                        {
                            new { distance = 367.0, duration = 50.0, type = 11, name = "-", way_points = new[] { 0, 1 } },
                            new { distance = 1033.0, duration = 140.0, type = 0, name = "-", way_points = new[] { 1, 2 } },
                            new { distance = 0.0, duration = 0.0, type = 10, name = "-", way_points = new[] { 2, 2 } }
                        }
                    }
                }
            }
        }
    };

    /// <summary>Codifica una línea como polilínea de Google con 5 decimales, el formato de OpenRouteService.</summary>
    public static string Codificar(IEnumerable<(decimal Latitud, decimal Longitud)> puntos)
    {
        var texto = new StringBuilder();
        int latitudAnterior = 0, longitudAnterior = 0;
        foreach (var (latitud, longitud) in puntos)
        {
            var lat = (int)Math.Round(latitud * 100_000m);
            var lon = (int)Math.Round(longitud * 100_000m);
            Agregar(texto, lat - latitudAnterior);
            Agregar(texto, lon - longitudAnterior);
            (latitudAnterior, longitudAnterior) = (lat, lon);
        }
        return texto.ToString();
    }

    private static void Agregar(StringBuilder texto, int valor)
    {
        var resto = valor < 0 ? ~(valor << 1) : valor << 1;
        while (resto >= 0x20)
        {
            texto.Append((char)((0x20 | (resto & 0x1F)) + 63));
            resto >>= 5;
        }
        texto.Append((char)(resto + 63));
    }
}
