using System.Globalization;

namespace FlowBB.Infrastructure.Routing.Mzk;

/// <summary>Typ rozkładu, czyli kolumna w tabliczce przystankowej MZK.</summary>
internal enum MzkServiceType
{
    Weekday,
    WeekdaySchoolHoliday,
    Saturday,
    Sunday
}

/// <summary>
/// Rodzaj doby wyłącznie na potrzeby flag wyjątków. <see cref="FlagHoliday"/> to dokładnie te trzy dni,
/// o których mówią legendy `N`, `R` i `W`: 25 grudnia, Nowy Rok i pierwszy dzień Wielkanocy.
/// </summary>
internal enum MzkDayKind
{
    Ordinary,
    FlagHoliday
}

internal readonly record struct MzkServiceWindow(DateOnly Day, MzkServiceType Service, int FromSecond, int ToSecond);

/// <summary>
/// Mapuje datę lokalną na typ rozkładu i na rodzaj doby. To jedyne miejsce, w którym kalendarz MZK jest
/// interpretowany; zmiana którejkolwiek z tych reguł jest zmianą wyłącznie tej klasy.
/// </summary>
internal sealed class MzkServiceCalendar
{
    /// <summary>
    /// Najpóźniejszy odjazd linii nocnej w danych to 03:58, więc ogon nocny doby kończymy o 04:00.
    /// ZALOZENIE Z-1: odjazd należy do doby, nad którą jest wydrukowany, bez przesunięcia o 24 h.
    /// Dla N1 i N2 oba warianty dają dziś identyczny wynik, bo godziny są takie same we wszystkich kolumnach.
    /// </summary>
    internal const int NightTailEndSecond = 4 * 60 * 60;

    private readonly Dictionary<DateOnly, MzkServiceType> _services;

    internal MzkServiceCalendar(IEnumerable<KeyValuePair<DateOnly, MzkServiceType>> days)
    {
        _services = new Dictionary<DateOnly, MzkServiceType>(days);
        HasDays = _services.Count > 0;
        FirstDay = HasDays ? _services.Keys.Min() : default;
        LastDay = HasDays ? _services.Keys.Max() : default;
    }

    internal static MzkServiceCalendar Empty { get; } = new([]);

    internal bool HasDays { get; }

    internal DateOnly FirstDay { get; }

    internal DateOnly LastDay { get; }

    internal bool TryGetService(DateOnly day, out MzkServiceType service) => _services.TryGetValue(day, out service);

    /// <summary>Parsuje nazwę serwisu z pliku; nieznana wartość oznacza dzień do pominięcia, nie ciche `weekday`.</summary>
    internal static bool TryParseService(string value, out MzkServiceType service)
    {
        switch (value)
        {
            case "weekday": service = MzkServiceType.Weekday; return true;
            case "weekday_holiday": service = MzkServiceType.WeekdaySchoolHoliday; return true;
            case "saturday": service = MzkServiceType.Saturday; return true;
            case "sunday": service = MzkServiceType.Sunday; return true;
            default: service = default; return false;
        }
    }

    internal static bool TryParseDay(string value, out DateOnly day) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    /// <summary>
    /// Rodzaj doby na potrzeby flag `N`, `R` i `W`. Świadomie NIE korzysta z `public_holiday` z kalendarza:
    /// to pole obejmuje także 1 i 11 listopada oraz 26 grudnia, których legendy nie dotyczą, więc użycie go
    /// skasowałoby istniejące kursy wieczorne i pokazało nieistniejącą lukę powrotową.
    /// </summary>
    internal static MzkDayKind DayKindOf(DateOnly day) =>
        IsFlagHoliday(day) ? MzkDayKind.FlagHoliday : MzkDayKind.Ordinary;

    private static bool IsFlagHoliday(DateOnly day) =>
        (day.Month, day.Day) is (12, 25) or (1, 1) || day == EasterSunday(day.Year);

    /// <summary>Anonimowy algorytm gregoriański: deterministyczny, bez sieci i bez tabel.</summary>
    private static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var month = (h + l - (7 * m) + 114) / 31;
        var dayOfMonth = ((h + l - (7 * m) + 114) % 31) + 1;
        return new DateOnly(year, month, dayOfMonth);
    }

    /// <summary>
    /// Okna czasowe do przeszukania, licząc od podanej chwili. Szukanie powrotu po 23:25 musi objąć resztę
    /// doby i ogon nocny doby następnej, bo linie nocne mają godziny 0-3 wydrukowane w tej następnej dobie.
    /// </summary>
    internal List<MzkServiceWindow> WindowsFrom(DateOnly day, int fromSecondOfDay, int horizonSeconds)
    {
        var windows = new List<MzkServiceWindow>(2);
        if (TryGetService(day, out var service))
        {
            var to = Math.Min(MzkLocalTime.SecondsPerDay - 1, fromSecondOfDay + horizonSeconds);
            windows.Add(new MzkServiceWindow(day, service, fromSecondOfDay, to));
        }

        var spill = fromSecondOfDay + horizonSeconds - MzkLocalTime.SecondsPerDay;
        if (spill <= 0)
        {
            return windows;
        }

        var next = day.AddDays(1);
        if (TryGetService(next, out var nextService))
        {
            windows.Add(new MzkServiceWindow(next, nextService, 0, Math.Min(NightTailEndSecond, spill)));
        }

        return windows;
    }
}
