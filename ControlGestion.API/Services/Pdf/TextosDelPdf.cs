using System.Globalization;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services.Pdf;

/// <summary>
/// Lo que se repite en los PDF de los reportes (tarea 33): los montos, las cantidades y las fechas escritos como en el
/// panel (Bs, dd/mm/aaaa y la hora de Bolivia), y los nombres de los filtros elegidos para el encabezado.
/// </summary>
internal static class TextosDelPdf
{
    public const string SinDato = "—";

    public static string Monto(decimal monto) => FormatoBolivia.Monto(monto);

    public static string Cantidad(decimal cantidad) => FormatoBolivia.Cantidad(cantidad);

    public static string Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? SinDato : valor;

    /// <summary>Una fila de una tabla del PDF, celda por celda.</summary>
    public static IReadOnlyList<string> Fila(params string[] celdas) => celdas;

    /// <summary>Un instante guardado en UTC, como "29/10/2026 16:10" en la hora de Bolivia.</summary>
    public static string FechaHora(DateTime? utc) => utc is { } valor
        ? HoraBolivia.DesdeUtc(valor).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
        : SinDato;

    /// <summary>El día de Bolivia de un instante guardado en UTC, como "29/10/2026".</summary>
    public static string Fecha(DateTime? utc) => utc is { } valor ? FormatoBolivia.Fecha(HoraBolivia.DiaDe(valor)) : SinDato;

    /// <summary>"01/09/2026 al 29/10/2026", "Desde el 01/09/2026" o "Hasta el 29/10/2026"; sin fechas, null.</summary>
    public static string? Periodo(DateOnly? desde, DateOnly? hasta) => (desde, hasta) switch
    {
        ({ } d, { } h) => $"{FormatoBolivia.Fecha(d)} al {FormatoBolivia.Fecha(h)}",
        ({ } d, null) => $"Desde el {FormatoBolivia.Fecha(d)}",
        (null, { } h) => $"Hasta el {FormatoBolivia.Fecha(h)}",
        _ => null
    };

    /// <summary>Suma un filtro a la lista solo si se eligió.</summary>
    public static void Agregar(this List<FiltroPdf> filtros, string etiqueta, string? valor)
    {
        if (!string.IsNullOrWhiteSpace(valor)) filtros.Add(new FiltroPdf(etiqueta, valor));
    }

    public static async Task<string> NombreDeZonaAsync(this AppDbContext db, int id) =>
        await db.Zonas.Where(z => z.Id == id).Select(z => z.Nombre).FirstOrDefaultAsync() ?? SinDato;

    public static async Task<string> NombreDeUsuarioAsync(this AppDbContext db, string id) =>
        await db.Users.Where(u => u.Id == id).Select(u => u.NombreCompleto).FirstOrDefaultAsync() ?? SinDato;

    /// <summary>El nombre de un cliente solo si es de la cartera del usuario: el PDF no muestra el de uno ajeno.</summary>
    public static async Task<string> NombreDeClienteAsync(this AppDbContext db, int id, IUsuarioActual usuario, TimeProvider reloj) =>
        await db.ClientesVisiblesPara(usuario, reloj).Where(c => c.Id == id).Select(c => c.NombreCompleto).FirstOrDefaultAsync()
        ?? SinDato;
}
