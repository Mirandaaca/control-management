using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ControlGestion.API.Configuracion;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ControlGestion.API.Services;

/// <summary>
/// El camino por calles entre dos puntos (tarea 25), con OpenRouteService. Devuelve <see cref="CaminosDTO"/>, sin la forma
/// del proveedor: si se cambia de proveedor, cambia solo esta clase. La clave viaja en el encabezado Authorization, nunca
/// en la URL, y ningún mensaje del log la lleva.
/// </summary>
public class NavegacionService(HttpClient http, IOptions<NavegacionOptions> opciones, IMemoryCache cache,
    CupoDeNavegacion cupo, ILogger<NavegacionService> logger)
{
    public const string CodigoNoDisponible = "navegacion_no_disponible";
    public const string MensajeNoDisponible = "No se pudo calcular el camino. Sigue el mapa.";

    // El camino principal y hasta dos alternativas. Los factores son los del proveedor por defecto: una alternativa
    // comparte como mucho el 60 % del principal y tarda como mucho un 40 % más.
    private const int CaminosPedidos = 3;

    public static ServicioNoDisponibleException NoDisponible() => new(CodigoNoDisponible, MensajeNoDisponible);

    public async Task<CaminosDTO> CaminoAsync(decimal latitud, decimal longitud, decimal latitudDestino,
        decimal longitudDestino, MedioDeTransporte medio)
    {
        var configuracion = opciones.Value;
        if (string.IsNullOrWhiteSpace(configuracion.Clave)) throw NoDisponible();

        // OpenRouteService no tiene perfil de moto: la moto va por donde va un auto.
        var perfil = medio == MedioDeTransporte.Pie ? "foot-walking" : "driving-car";
        // Redondeado a unos 11 m: dos pedidos desde casi el mismo punto comparten el camino del caché.
        latitud = Math.Round(latitud, 4);
        longitud = Math.Round(longitud, 4);
        var claveDelCache = string.Create(CultureInfo.InvariantCulture,
            $"camino:{perfil}:{latitud}:{longitud}:{latitudDestino}:{longitudDestino}");
        if (cache.TryGetValue(claveDelCache, out CaminosDTO? guardado)) return guardado!;

        if (!cupo.Consumir())
        {
            logger.LogWarning("Se alcanzó el tope diario de {Tope} caminos: la navegación vuelve mañana.",
                configuracion.TopeDiario);
            throw NoDisponible();
        }

        var caminos = await PedirAsync(configuracion.Clave, perfil, [[longitud, latitud], [longitudDestino, latitudDestino]]);
        cache.Set(claveDelCache, caminos, TimeSpan.FromSeconds(configuracion.SegundosDeCache));
        return caminos;
    }

    private async Task<CaminosDTO> PedirAsync(string clave, string perfil, decimal[][] coordenadas)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, $"v2/directions/{perfil}/json")
        {
            Content = JsonContent.Create(new
            {
                coordinates = coordenadas,
                language = "es",
                instructions = true,
                instructions_format = "text",
                units = "m",
                alternative_routes = new { target_count = CaminosPedidos, share_factor = 0.6, weight_factor = 1.4 }
            })
        };
        pedido.Headers.TryAddWithoutValidation("Authorization", clave);

        RespuestaDelProveedor? respuesta;
        try
        {
            using var recibida = await http.SendAsync(pedido);
            if (!recibida.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenRouteService respondió {Estado} (código {Codigo}).",
                    (int)recibida.StatusCode, await CodigoDeErrorAsync(recibida));
                throw NoDisponible();
            }
            respuesta = await recibida.Content.ReadFromJsonAsync<RespuestaDelProveedor>();
        }
        // El tiempo agotado (8 s) llega como una cancelación.
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException)
        {
            logger.LogWarning("OpenRouteService no respondió o respondió algo que no se entiende: {Tipo}: {Mensaje}",
                e.GetType().Name, e.Message);
            throw NoDisponible();
        }

        var caminos = respuesta?.Routes?.Select(Convertir).Where(c => c != null).Select(c => c!).ToList() ?? [];
        if (caminos.Count == 0)
        {
            logger.LogWarning("OpenRouteService respondió sin ningún camino que se pueda dibujar.");
            throw NoDisponible();
        }
        return new CaminosDTO(caminos[0], caminos[1..]);
    }

    /// <summary>El código de error del proveedor ({"error": {"code": 2010}}), sin el resto del cuerpo.</summary>
    private static async Task<string> CodigoDeErrorAsync(HttpResponseMessage respuesta)
    {
        try
        {
            var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
            return cuerpo.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("code", out var codigo)
                ? codigo.ToString()
                : "sin código";
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or HttpRequestException)
        {
            return "sin código";
        }
    }

    private static CaminoDTO? Convertir(RutaDelProveedor ruta)
    {
        var puntos = Decodificar(ruta.Geometry ?? string.Empty);
        if (puntos.Count == 0) return null;

        var pasos = (ruta.Segments ?? []).SelectMany(s => s.Steps ?? []).Select(paso =>
        {
            var indice = Math.Clamp(paso.WayPoints is [var desde, ..] ? desde : 0, 0, puntos.Count - 1);
            var giro = GiroDe(paso.Type);
            var calle = Calle(paso.Name);
            return new PasoDTO(Indicacion(giro, calle, paso.ExitNumber), calle, giro, Redondear(paso.Distance),
                Redondear(paso.Duration), puntos[indice], indice);
        }).ToList();

        return new CaminoDTO(Redondear(ruta.Summary?.Distance ?? 0), Redondear(ruta.Summary?.Duration ?? 0), puntos, pasos);
    }

    private static int Redondear(double valor) => (int)Math.Round(valor, MidpointRounding.AwayFromZero);

    // Los tipos de paso de OpenRouteService (0 a 13): https://giscience.github.io/openrouteservice/api-reference/endpoints/directions/instruction-types
    private static Giro GiroDe(int tipo) => tipo switch
    {
        0 => Giro.Izquierda,
        1 => Giro.Derecha,
        2 => Giro.IzquierdaCerrada,
        3 => Giro.DerechaCerrada,
        4 => Giro.IzquierdaLeve,
        5 => Giro.DerechaLeve,
        7 => Giro.Rotonda,
        8 => Giro.SalirDeRotonda,
        9 => Giro.Retorno,
        10 => Giro.Llegada,
        11 => Giro.Salida,
        12 => Giro.MantenerIzquierda,
        13 => Giro.MantenerDerecha,
        _ => Giro.Recto
    };

    // Una calle sin nombre llega como "-".
    private static string? Calle(string? nombre) =>
        string.IsNullOrWhiteSpace(nombre) || nombre.Trim() == "-" ? null : nombre.Trim();

    /// <summary>
    /// La indicación de tú, como el resto de la app ("Gira a la derecha en Calle 4"). Se arma aquí y no se usa la del
    /// proveedor, que trata de usted y cambiaría con otro proveedor.
    /// </summary>
    private static string Indicacion(Giro giro, string? calle, int? salida)
    {
        string En(string texto) => calle == null ? texto : $"{texto} en {calle}";
        string Por(string texto) => calle == null ? texto : $"{texto} por {calle}";
        string Hacia(string texto) => calle == null ? texto : $"{texto} hacia {calle}";

        return giro switch
        {
            Giro.Salida => calle == null ? "Sigue el camino marcado" : $"Sigue por {calle}",
            Giro.Izquierda => En("Gira a la izquierda"),
            Giro.Derecha => En("Gira a la derecha"),
            Giro.IzquierdaCerrada => En("Gira bruscamente a la izquierda"),
            Giro.DerechaCerrada => En("Gira bruscamente a la derecha"),
            Giro.IzquierdaLeve => En("Gira levemente a la izquierda"),
            Giro.DerechaLeve => En("Gira levemente a la derecha"),
            Giro.MantenerIzquierda => Por("Mantente a la izquierda"),
            Giro.MantenerDerecha => Por("Mantente a la derecha"),
            Giro.Rotonda => Hacia(salida is > 0 ? $"En la rotonda, toma la {salida}.ª salida" : "Entra a la rotonda"),
            Giro.SalirDeRotonda => Hacia("Sal de la rotonda"),
            Giro.Retorno => "Da la vuelta en U",
            Giro.Llegada => "Llegas a tu destino",
            _ => Por("Sigue recto")
        };
    }

    /// <summary>La línea del camino, que llega como polilínea codificada con 5 decimales (el formato de Google).</summary>
    private static List<PuntoDTO> Decodificar(string polilinea)
    {
        var puntos = new List<PuntoDTO>();
        int posicion = 0, latitud = 0, longitud = 0;
        while (posicion < polilinea.Length)
        {
            if (!SiguienteValor(polilinea, ref posicion, out var deltaLatitud)
                || !SiguienteValor(polilinea, ref posicion, out var deltaLongitud))
                return [];
            latitud += deltaLatitud;
            longitud += deltaLongitud;
            puntos.Add(new PuntoDTO(latitud / 100_000m, longitud / 100_000m));
        }
        return puntos;
    }

    private static bool SiguienteValor(string polilinea, ref int posicion, out int valor)
    {
        int resultado = 0, desplazamiento = 0, trozo;
        do
        {
            if (posicion >= polilinea.Length)
            {
                valor = 0;
                return false;
            }
            trozo = polilinea[posicion++] - 63;
            resultado |= (trozo & 0x1F) << desplazamiento;
            desplazamiento += 5;
        } while (trozo >= 0x20);
        valor = (resultado & 1) != 0 ? ~(resultado >> 1) : resultado >> 1;
        return true;
    }

    // Solo lo que se usa de la respuesta de /v2/directions/{perfil}/json.
    private sealed record RespuestaDelProveedor(List<RutaDelProveedor>? Routes);

    private sealed record RutaDelProveedor(ResumenDelProveedor? Summary, string? Geometry, List<SegmentoDelProveedor>? Segments);

    private sealed record ResumenDelProveedor(double Distance, double Duration);

    private sealed record SegmentoDelProveedor(List<PasoDelProveedor>? Steps);

    private sealed record PasoDelProveedor(
        double Distance,
        double Duration,
        int Type,
        string? Name,
        [property: JsonPropertyName("way_points")] int[]? WayPoints,
        [property: JsonPropertyName("exit_number")] int? ExitNumber);
}
