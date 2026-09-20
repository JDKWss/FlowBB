using System.Text;
using FlowBB.Domain.Common;
using FlowBB.Infrastructure.Routing.Mzk;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowBB.Infrastructure.Tests.Routing.Mzk;

/// <summary>
/// Maly rozklad z jawnymi godzinami, zbudowany jako JSON w ksztalcie plikow z data/gtfs/mzk/parsed/. Linia T1 jedzie
/// A-B-C-D co 30 minut (odcinki po 3 minuty), a kierunek powrotny T1 Back D-C-B-A ma godziny zadane przez test.
/// </summary>
internal static class SyntheticTimetable
{
    internal static readonly GeoPoint StopA = new(49.8100, 19.0300);
    internal static readonly GeoPoint StopB = new(49.8150, 19.0400);
    internal static readonly GeoPoint StopC = new(49.8200, 19.0500);
    internal static readonly GeoPoint StopD = new(49.8250, 19.0600);

    internal static readonly string[] Services = ["weekday", "weekday_holiday", "saturday", "sunday"];

    internal sealed record Dep(int Sec, string[]? Flags = null, bool DepotRun = false);

    internal sealed record Page(string Line, string Direction, int Seq, string Stop, Dep[] Deps);

    internal static int Sec(int hour, int minute) => (hour * 3600) + (minute * 60);

    /// <summary>Godziny co 30 minut od 08:00 do 23:30 przesuniete o offsetMinutes; skipHours to sloty bazowe (np. 1130) do pominiecia, przed przesunieciem.</summary>
    internal static Dep[] HalfHourly(int offsetMinutes, params int[] skipHours) =>
    [
        .. Enumerable.Range(8, 16)
            .SelectMany(hour => new[] { 0, 30 }.Select(minute => (hour, minute)))
            .Where(slot => !skipHours.Contains((slot.hour * 100) + slot.minute))
            .Select(slot => new Dep(Sec(slot.hour, slot.minute) + (offsetMinutes * 60)))
    ];

    internal static Dep[] At(params (int Hour, int Minute)[] times) =>
        [.. times.Select(time => new Dep(Sec(time.Hour, time.Minute)))];

    internal static Page[] Forward(params int[] skipAtC) =>
    [
        new("T1", "Forward", 1, "A", HalfHourly(0)),
        new("T1", "Forward", 2, "B", HalfHourly(3)),
        new("T1", "Forward", 3, "C", HalfHourly(6, skipAtC)),
        new("T1", "Forward", 4, "D", HalfHourly(9))
    ];

    /// <summary>Kierunek powrotny; stopMinutes to minuty odjazdu z D w kazdej godzinie 08-23, odcinki po 3 minuty.</summary>
    internal static Page[] Back(params int[] minutesPastEachHour)
    {
        Dep[] Hourly(int offset) =>
        [
            .. Enumerable.Range(8, 16)
                .SelectMany(hour => minutesPastEachHour.Select(minute => new Dep(Sec(hour, minute) + (offset * 60))))
        ];

        return
        [
            new("T1", "Back", 1, "D", Hourly(0)),
            new("T1", "Back", 2, "C", Hourly(3)),
            new("T1", "Back", 3, "B", Hourly(6)),
            new("T1", "Back", 4, "A", Hourly(9))
        ];
    }

    /// <summary>Linia nocna D-A z czterema odjazdami po polnocy; dwa przystanki i cztery odjazdy wystarczaja na czas jazdy.</summary>
    internal static Page[] Night() =>
    [
        new("N1", "Back", 1, "D", At((0, 30), (1, 30), (2, 30), (3, 30))),
        new("N1", "Back", 2, "A", At((0, 40), (1, 40), (2, 40), (3, 40)))
    ];

    internal static readonly string[] CalendarDays =
    [
        "2026-09-19|saturday", "2026-09-20|sunday", "2026-09-21|weekday", "2026-09-22|weekday", "2026-09-23|weekday",
        "2026-11-01|sunday", "2026-11-02|weekday", "2026-12-24|weekday", "2026-12-25|sunday", "2026-12-26|sunday"
    ];

    internal static MzkTimetable Build(IEnumerable<Page> pages, bool withStops = true, string[]? calendar = null) =>
        MzkTimetableLoader.Load(
            Stream(DeparturesJson(pages)),
            Stream(CalendarJson(calendar ?? CalendarDays)),
            withStops ? Stream(StopsJson()) : null,
            NullLogger.Instance);

    internal static MzkTimetable Standard(params int[] minutesBack) =>
        Build([.. Forward(), .. Back(minutesBack.Length == 0 ? [5, 35] : minutesBack)]);

    private static MemoryStream Stream(string json) => new(Encoding.UTF8.GetBytes(json));

    internal static string DeparturesJson(IEnumerable<Page> pages)
    {
        var builder = new StringBuilder("[");
        foreach (var page in pages)
        {
            builder.Append("{\"line\":\"").Append(page.Line).Append("\",\"seq\":").Append(page.Seq)
                .Append(",\"stop_name\":\"").Append(page.Stop).Append("\",\"direction\":\"").Append(page.Direction)
                .Append("\",\"departures\":{");
            builder.Append(string.Join(",", Services.Select(service => $"\"{service}\":{DepsJson(page.Deps)}")));
            builder.Append("}},");
        }

        return builder.Append(']').ToString().Replace(",]", "]");
    }

    private static string DepsJson(Dep[] deps) =>
        "[" + string.Join(",", deps.Select(dep =>
            $"{{\"sec\":{dep.Sec},\"flags\":[{string.Join(",", (dep.Flags ?? []).Select(flag => $"\"{flag}\""))}],\"depot_run\":{dep.DepotRun.ToString().ToLowerInvariant()}}}")) + "]";

    internal static string CalendarJson(string[] days) =>
        "[" + string.Join(",", days.Select(day => day.Split('|'))
            .Select(parts => $"{{\"day\":\"{parts[0]}\",\"service\":\"{parts[1]}\",\"public_holiday\":false}}")) + "]";

    internal static string StopsJson() =>
        "[" + string.Join(",", new[] { ("A", StopA), ("B", StopB), ("C", StopC), ("D", StopD) }
            .Select(stop => $"{{\"stop_name\":\"{stop.Item1}\",\"latitude\":{stop.Item2.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"longitude\":{stop.Item2.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}")) + "]";
}
