namespace ControlGestion.API.Configuracion;

/// <summary>
/// Las fechas se guardan en UTC, pero "hoy", "el día 15" o "este año" se cuentan en la hora de Bolivia
/// (America/La_Paz, UTC−4, sin horario de verano). Una venta de las 21:00 del 15 en Santa Cruz se guarda
/// como la 01:00 del 16 en UTC y, aun así, pertenece al día 15.
/// </summary>
public static class HoraBolivia
{
    public const string IdDeZona = "America/La_Paz";

    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById(IdDeZona);

    public static DateTime DesdeUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zona);

    public static DateOnly Hoy(TimeProvider reloj) => DiaDe(reloj.GetUtcNow().UtcDateTime);

    /// <summary>El día de Bolivia al que pertenece un instante guardado en UTC.</summary>
    public static DateOnly DiaDe(DateTime utc) => DateOnly.FromDateTime(DesdeUtc(utc));

    /// <summary>El instante UTC en que empieza ese día en Bolivia.</summary>
    public static DateTime InicioDelDiaUtc(DateOnly dia) =>
        TimeZoneInfo.ConvertTimeToUtc(dia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Zona);

    /// <summary>El instante UTC en que empieza el día siguiente: se usa como límite exclusivo (&lt;).</summary>
    public static DateTime FinDelDiaUtc(DateOnly dia) => InicioDelDiaUtc(dia.AddDays(1));

    /// <summary>El instante UTC en que termina hoy en Bolivia (empieza mañana).</summary>
    public static DateTime FinDeHoyUtc(TimeProvider reloj) => FinDelDiaUtc(Hoy(reloj));

    /// <summary>
    /// Una fecha y hora elegida por el usuario. Sin zona (lo que manda un campo datetime-local del panel) se entiende
    /// como hora de Bolivia; si trae zona ("Z" o un desfase), se respeta.
    /// </summary>
    public static DateTime AUtc(DateTime fecha) => fecha.Kind switch
    {
        DateTimeKind.Utc => fecha,
        DateTimeKind.Local => fecha.ToUniversalTime(),
        _ => TimeZoneInfo.ConvertTimeToUtc(fecha, Zona),
    };
}
