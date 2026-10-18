namespace ControlGestion.API.Configuracion;

/// <summary>
/// Las fechas se guardan en UTC, pero "hoy", "el día 15" o "este año" se cuentan en la hora de Bolivia
/// (America/La_Paz, UTC−4, sin horario de verano). Una venta de las 21:00 del 15 en Santa Cruz se guarda
/// como la 01:00 del 16 en UTC y, aun así, pertenece al día 15.
/// </summary>
public static class HoraBolivia
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/La_Paz");

    public static DateTime DesdeUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zona);

    public static DateOnly Hoy(TimeProvider reloj) => DateOnly.FromDateTime(DesdeUtc(reloj.GetUtcNow().UtcDateTime));

    /// <summary>El instante UTC en que empieza ese día en Bolivia.</summary>
    public static DateTime InicioDelDiaUtc(DateOnly dia) =>
        TimeZoneInfo.ConvertTimeToUtc(dia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Zona);

    /// <summary>El instante UTC en que empieza el día siguiente: se usa como límite exclusivo (&lt;).</summary>
    public static DateTime FinDelDiaUtc(DateOnly dia) => InicioDelDiaUtc(dia.AddDays(1));
}
