namespace SagaSales.Shared.Time;

/// <summary>
/// Días locales del cliente expresados en UTC (spec 006). Los servicios guardan UTC; el cajero piensa en
/// "hoy" en su hora local, así que el navegador manda su desfase y aquí se convierte.
/// </summary>
public readonly record struct LocalDayRange(DateTime FromUtc, DateTime ToUtc, DateOnly FirstDay, DateOnly LastDay, int OffsetMinutes)
{
    public const int MaxDays = 62;

    /// <summary>Los últimos <paramref name="days"/> días locales, hoy incluido.</summary>
    /// <param name="offsetMinutes">Desfase local respecto a UTC; Ecuador es −300.</param>
    public static LocalDayRange LastDays(int days, int offsetMinutes, DateTime utcNow)
    {
        if (days is < 1 or > MaxDays)
        {
            throw new ArgumentOutOfRangeException(nameof(days), days, $"Entre 1 y {MaxDays} días.");
        }

        if (offsetMinutes is < -840 or > 840)
        {
            throw new ArgumentOutOfRangeException(nameof(offsetMinutes), offsetMinutes, "Desfase horario fuera de rango.");
        }

        var offset = TimeSpan.FromMinutes(offsetMinutes);
        var today = DateOnly.FromDateTime(DateTime.SpecifyKind(utcNow, DateTimeKind.Unspecified) + offset);
        var first = today.AddDays(1 - days);

        var fromUtc = DateTime.SpecifyKind(first.ToDateTime(TimeOnly.MinValue) - offset, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(today.AddDays(1).ToDateTime(TimeOnly.MinValue) - offset, DateTimeKind.Utc);

        return new LocalDayRange(fromUtc, toUtc, first, today, offsetMinutes);
    }

    /// <summary>Solo el día de hoy dentro de este rango.</summary>
    public LocalDayRange Today()
    {
        var offset = TimeSpan.FromMinutes(OffsetMinutes);
        var fromUtc = DateTime.SpecifyKind(LastDay.ToDateTime(TimeOnly.MinValue) - offset, DateTimeKind.Utc);
        return new LocalDayRange(fromUtc, ToUtc, LastDay, LastDay, OffsetMinutes);
    }

    /// <summary>Cada día del rango, en orden.</summary>
    public IEnumerable<DateOnly> EachDay()
    {
        for (var day = FirstDay; day <= LastDay; day = day.AddDays(1))
        {
            yield return day;
        }
    }
}
