using System.Globalization;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ControlGestion.API.Services;

/// <summary>El archivo de una subida, sin depender de HTTP: el controlador lo arma con el IFormFile.</summary>
public record ArchivoRecibido(string Nombre, long Tamano, Stream Contenido);

/// <summary>
/// Las fotos de los eventos (4.2) y los comprobantes de pago (3.1). Se validan por su contenido, se guardan en la carpeta
/// de archivos con un nombre que pone el servidor y se abren solo con el permiso y la cartera del registro al que
/// pertenecen.
/// </summary>
public class EvidenciaService(AppDbContext db, IUsuarioActual usuario, IOptions<AlmacenamientoOptions> opciones,
    TimeProvider reloj, ILogger<EvidenciaService> logger)
{
    public const long TamanoMaximo = 10 * 1024 * 1024;

    /// <summary>Los permisos que abren una foto de un evento: los mismos que abren el evento (EventosController).</summary>
    private static readonly string[] LeenEventos =
    [
        Permisos.Eventos.ConsultarEventos, Permisos.Eventos.RegistrarEvento, Permisos.Eventos.ModificarEvento,
        Permisos.Eventos.ResolverEvento, Permisos.Eventos.AgregarFoto, Permisos.Eventos.EliminarFoto, Permisos.Eventos.VerMapa,
    ];

    /// <summary>Los permisos que abren un comprobante: los que abren la cobranza, y quien registra o anula sus pagos.</summary>
    private static readonly string[] LeenCobranzas =
    [
        Permisos.Cobranzas.ConsultarDeudas, Permisos.Cobranzas.ConsultarPagos, Permisos.Cobranzas.RegistrarPago,
        Permisos.Cobranzas.AnularPago,
    ];

    private static ReadOnlySpan<byte> FirmaPng => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Valida la foto y la guarda en el disco. Devuelve la evidencia sin agregarla a la base: lo hace quien la llama, en su
    /// misma transacción, y si después algo falla, borra el archivo con <see cref="BorrarArchivo"/>.
    /// </summary>
    public async Task<Evidencia> GuardarFotoAsync(ArchivoRecibido archivo, decimal? latitud = null, decimal? longitud = null,
        DateTime? fechaCaptura = null)
    {
        if (archivo.Tamano == 0)
            throw new ReglaDeNegocioException("archivo_vacio", "El archivo está vacío: elige una foto.");
        if (archivo.Tamano > TamanoMaximo)
            throw new ReglaDeNegocioException("archivo_muy_grande",
                $"La foto pesa {Megas(archivo.Tamano)}; el máximo es {Megas(TamanoMaximo)}.");

        // El tipo sale de los primeros bytes, no del Content-Type ni de la extensión, que los elige quien sube el archivo:
        // un .html declarado image/png se guardaba y se servía como página (T7.05).
        var cabecera = new byte[12];
        var leidos = await archivo.Contenido.ReadAtLeastAsync(cabecera, cabecera.Length, throwOnEndOfStream: false);
        var (tipo, extension) = Formato(cabecera.AsSpan(0, leidos))
            ?? throw new ReglaDeNegocioException("archivo_no_es_imagen", "El archivo no es una foto: sube una imagen JPEG, PNG o WebP.");

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var ruta = $"{ahora:yyyy}/{ahora:MM}/{Guid.NewGuid():N}{extension}";
        var destino = RutaEnDisco(ruta);
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        await using (var salida = new FileStream(destino, FileMode.CreateNew))
        {
            await salida.WriteAsync(cabecera.AsMemory(0, leidos));
            await archivo.Contenido.CopyToAsync(salida);
        }

        return new Evidencia
        {
            NombreArchivo = NombreParaMostrar(archivo.Nombre),
            RutaAlmacenamiento = ruta,
            TipoMIME = tipo,
            TamanoBytes = archivo.Tamano,
            Latitud = latitud,
            Longitud = longitud,
            FechaCaptura = fechaCaptura,
            FechaSubida = ahora,
            SubidoPorId = usuario.Id
        };
    }

    /// <summary>
    /// La foto para mostrarla, con el tipo de su firma. Se abre con el permiso de leer su registro (el evento o la cobranza
    /// del pago) y si ese registro es de la cartera del usuario. Si no, responde 404, como si no existiera.
    /// </summary>
    public async Task<(Stream Contenido, string Tipo)> AbrirAsync(int id)
    {
        var evidencia = await db.Evidencias
            .Where(e => e.Id == id)
            .Select(e => new
            {
                e.RutaAlmacenamiento,
                e.TipoMIME,
                e.EventoId,
                CobranzaId = e.Pago == null ? (int?)null : e.Pago.CobranzaId
            })
            .FirstOrDefaultAsync();
        if (evidencia == null || !await PuedeVerAsync(evidencia.EventoId, evidencia.CobranzaId))
            throw new NoEncontradoException("No se encontró la foto.");

        var ruta = RutaEnDisco(evidencia.RutaAlmacenamiento);
        if (!File.Exists(ruta))
            throw new NoEncontradoException("La foto ya no está en el servidor.");
        return (File.OpenRead(ruta), evidencia.TipoMIME);
    }

    private async Task<bool> PuedeVerAsync(int? eventoId, int? cobranzaId)
    {
        if (eventoId is { } idEvento)
            return LeenEventos.Any(usuario.Tiene) && await db.Eventos.VisiblesPara(usuario).AnyAsync(e => e.Id == idEvento);
        if (cobranzaId is { } idCobranza)
            return LeenCobranzas.Any(usuario.Tiene) && await db.Cobranzas.VisiblesPara(usuario).AnyAsync(c => c.Id == idCobranza);
        return false;
    }

    /// <summary>
    /// Borra el archivo de una evidencia. Se llama después de guardar en la base: si falla, queda un archivo sin registro,
    /// que nadie puede abrir, y se anota en el log.
    /// </summary>
    public void BorrarArchivo(string rutaAlmacenamiento)
    {
        try
        {
            File.Delete(RutaEnDisco(rutaAlmacenamiento));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "No se pudo borrar el archivo {Ruta} de la carpeta de archivos.", rutaAlmacenamiento);
        }
    }

    // La ruta la arma el servidor, pero se revisa igual que no salga de la carpeta de archivos.
    private string RutaEnDisco(string rutaAlmacenamiento)
    {
        var carpeta = Path.GetFullPath(opciones.Value.Ruta);
        var ruta = Path.GetFullPath(Path.Combine(carpeta, rutaAlmacenamiento));
        if (!ruta.StartsWith(carpeta + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException($"La ruta {rutaAlmacenamiento} sale de la carpeta de archivos.");
        return ruta;
    }

    private static (string Tipo, string Extension)? Formato(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ("image/jpeg", ".jpg");
        if (b.StartsWith(FirmaPng)) return ("image/png", ".png");
        if (b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8) && b[8..12].SequenceEqual("WEBP"u8)) return ("image/webp", ".webp");
        return null;
    }

    private static string NombreParaMostrar(string nombre)
    {
        var limpio = Path.GetFileName(nombre).Trim();
        if (limpio.Length == 0) return "foto";
        return limpio.Length <= 255 ? limpio : limpio[..255];
    }

    private static string Megas(long bytes) =>
        $"{(bytes / (1024d * 1024)).ToString("0.#", CultureInfo.GetCultureInfo("es-BO"))} MB";
}
