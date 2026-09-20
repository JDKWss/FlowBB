namespace FlowBB.Infrastructure.Routing.Mzk;

/// <summary>
/// Jedyne miejsce konwersji między chwilą a parą (doba lokalna, sekunda doby) w strefie demo Bielska-Białej.
/// Rozkłady MZK są wydrukowane w czasie lokalnym, więc dobór kursu musi się odbywać w tym samym czasie.
/// </summary>
internal static class MzkLocalTime
{
    internal const int SecondsPerDay = 24 * 60 * 60;

    private static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    internal static (DateOnly Day, int SecondOfDay) ToLocal(DateTimeOffset instant)
    {
        var local = TimeZoneInfo.ConvertTime(instant, Warsaw);
        return (DateOnly.FromDateTime(local.Date), (int)local.TimeOfDay.TotalSeconds);
    }

    /// <summary>
    /// Zamienia dobę lokalną i sekundę doby na chwilę. W noc cofnięcia zegara godzina jest niejednoznaczna;
    /// <see cref="TimeZoneInfo.GetUtcOffset(DateTime)"/> wybiera wtedy offset standardowy (ZALOZENIE Z-4).
    /// </summary>
    internal static DateTimeOffset ToInstant(DateOnly day, int secondOfDay)
    {
        var local = day.ToDateTime(TimeOnly.MinValue).AddSeconds(secondOfDay);
        return new DateTimeOffset(local, Warsaw.GetUtcOffset(local));
    }
}
