using System.Reflection;
using System.Text.Json;
using FlowBB.Domain.Common;
using Microsoft.Extensions.Logging;

namespace FlowBB.Infrastructure.Routing.Mzk;

/// <summary>
/// Buduje rozkład z plików <c>data/gtfs/mzk/parsed/</c> osadzonych w assembly. Zasada: lepiej pominąć odjazd,
/// którego nie umiemy zinterpretować, niż zaproponować kurs, który nie dojedzie.
/// </summary>
internal static class MzkTimetableLoader
{
    internal const string DeparturesResource = "FlowBB.Mzk.departures.json";
    internal const string CalendarResource = "FlowBB.Mzk.calendar_days.json";
    internal const string StopsResource = "FlowBB.Mzk.stops.json";

    // Ramka Bielska-Białej. Chroni przed cichym przyjęciem pliku o innym schemacie: współrzędne 0,0 dałyby
    // "brak przystanku w zasięgu" zamiast błędu ładowania.
    private const double MinLatitude = 49.6;
    private const double MaxLatitude = 50.0;
    private const double MinLongitude = 18.8;
    private const double MaxLongitude = 19.4;

    // Czas odcinka przyjmujemy tylko wtedy, gdy większość odjazdów daje tę samą różnicę. Poniżej progów odcinek
    // jest niepewny i kurs przez niego jest pomijany, a nie łatany szacunkiem.
    private const int MinHopSamples = 4;
    private const int MaxHopMinutes = 15;

    private static readonly HashSet<string> KnownFlags = ["#", "K", "D", "N", "R", "W", "Ś"];

    /// <summary>Wersja produkcyjna: nigdy nie rzuca, bo Lazy zapamiętałby wyjątek i każde żądanie dawałoby 500.</summary>
    internal static MzkTimetable LoadOrEmpty(ILogger logger)
    {
        try
        {
            using var departures = OpenResource(DeparturesResource, required: true)!;
            using var calendar = OpenResource(CalendarResource, required: true)!;
            using var stops = OpenResource(StopsResource, required: false);
            return Load(departures, calendar, stops, logger);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or IOException)
        {
            logger.LogError(error, "MZK timetable could not be loaded; public transport falls back to the demo planner.");
            return MzkTimetable.Empty;
        }
    }

    internal static MzkTimetable Load(Stream departures, Stream calendarDays, Stream? stops, ILogger logger)
    {
        var pages = JsonSerializer.Deserialize<List<MzkDeparturePageJson>>(departures, MzkTimetableJson.Options) ?? [];
        var days = JsonSerializer.Deserialize<List<MzkCalendarDayJson>>(calendarDays, MzkTimetableJson.Options) ?? [];
        var stopRecords = stops is null
            ? []
            : JsonSerializer.Deserialize<List<MzkStopJson>>(stops, MzkTimetableJson.Options) ?? [];

        var coordinates = ValidStops(stopRecords, logger);
        var patterns = BuildPatterns(pages, logger);
        var withoutCoordinates = patterns
            .SelectMany(pattern => pattern.Stops.Select(stop => stop.Name))
            .Distinct()
            .Count(name => !coordinates.ContainsKey(name));
        if (withoutCoordinates > 0)
        {
            logger.LogInformation("MZK timetable: {Count} stops have no coordinates and are never offered as boarding stops.", withoutCoordinates);
        }

        return new MzkTimetable(patterns, new MzkStopIndex(coordinates), BuildCalendar(days, logger));
    }

    private static Stream? OpenResource(string name, bool required)
    {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        return stream is null && required
            ? throw new InvalidOperationException($"Embedded MZK resource {name} was not found.")
            : stream;
    }

    private static Dictionary<string, GeoPoint> ValidStops(List<MzkStopJson> records, ILogger logger)
    {
        var valid = new Dictionary<string, GeoPoint>(StringComparer.Ordinal);
        var rejected = 0;
        foreach (var record in records)
        {
            if (IsInsideBielsko(record) && !string.IsNullOrWhiteSpace(record.StopName))
            {
                valid[record.StopName] = new GeoPoint(record.Latitude, record.Longitude);
            }
            else
            {
                rejected++;
            }
        }

        if (rejected > 0)
        {
            logger.LogWarning("MZK timetable: {Count} stop records were rejected (missing name or coordinates outside Bielsko-Biala).", rejected);
        }

        return valid;
    }

    private static bool IsInsideBielsko(MzkStopJson stop) =>
        stop.Latitude is >= MinLatitude and <= MaxLatitude && stop.Longitude is >= MinLongitude and <= MaxLongitude;

    private static MzkServiceCalendar BuildCalendar(List<MzkCalendarDayJson> days, ILogger logger)
    {
        var parsed = new List<KeyValuePair<DateOnly, MzkServiceType>>(days.Count);
        foreach (var record in days)
        {
            if (MzkServiceCalendar.TryParseDay(record.Day, out var day) && MzkServiceCalendar.TryParseService(record.Service, out var service))
            {
                parsed.Add(new KeyValuePair<DateOnly, MzkServiceType>(day, service));
            }
            else
            {
                logger.LogWarning("MZK calendar: day {Day} with service {Service} was skipped.", record.Day, record.Service);
            }
        }

        return new MzkServiceCalendar(parsed);
    }

    private static List<MzkRoutePattern> BuildPatterns(List<MzkDeparturePageJson> pages, ILogger logger)
    {
        var skipped = 0;
        var patterns = pages
            .GroupBy(page => (page.Line, page.Direction))
            .OrderBy(group => group.Key.Line, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Direction, StringComparer.Ordinal)
            .Select(group => BuildPattern(group.Key.Line, group.Key.Direction, [.. group.OrderBy(page => page.Seq)], ref skipped))
            .ToList();

        if (skipped > 0)
        {
            logger.LogWarning("MZK timetable: {Count} departures with an unknown flag were skipped.", skipped);
        }

        return patterns;
    }

    private static MzkRoutePattern BuildPattern(string line, string direction, List<MzkDeparturePageJson> pages, ref int skipped)
    {
        var stops = new List<MzkPatternStop>(pages.Count);
        foreach (var page in pages)
        {
            stops.Add(new MzkPatternStop(page.StopName, BuildBoard(page.Departures, ref skipped)));
        }

        return new MzkRoutePattern(line, direction, stops, BuildHops(stops));
    }

    private static MzkDepartureBoard BuildBoard(MzkServiceDeparturesJson departures, ref int skipped)
    {
        var profiles = new int[Enum.GetValues<MzkServiceType>().Length * 2][];
        foreach (var service in Enum.GetValues<MzkServiceType>())
        {
            foreach (var kind in Enum.GetValues<MzkDayKind>())
            {
                profiles[MzkDepartureBoard.ProfileIndex(service, kind)] = Profile(departures.For(service), kind, ref skipped);
            }
        }

        return new MzkDepartureBoard(profiles);
    }

    private static int[] Profile(List<MzkDepartureJson> departures, MzkDayKind kind, ref int skipped)
    {
        var seconds = new List<int>(departures.Count);
        foreach (var departure in departures)
        {
            if (departure.DepotRun)
            {
                continue;
            }

            if (!departure.Flags.All(KnownFlags.Contains))
            {
                skipped++;
                continue;
            }

            if (RunsOn(departure.Flags, kind))
            {
                seconds.Add(departure.Sec);
            }
        }

        return [.. seconds.Distinct().Order()];
    }

    // N i R: nie kursuje w dniach z legendy. W i Ś: kursuje tylko w dniach z legendy. Flagi K i D (kurs skrócony)
    // nie filtrują tu niczego: kurs, który nie dojeżdża, odrzuca łańcuch godzin w MzkRoutePattern.FollowTrip.
    private static bool RunsOn(List<string> flags, MzkDayKind kind) => kind switch
    {
        MzkDayKind.FlagHoliday => !flags.Contains("N") && !flags.Contains("R"),
        _ => !flags.Contains("W") && !flags.Contains("Ś")
    };

    private static int[][] BuildHops(List<MzkPatternStop> stops)
    {
        var hops = new int[Enum.GetValues<MzkServiceType>().Length][];
        foreach (var service in Enum.GetValues<MzkServiceType>())
        {
            hops[(int)service] = new int[Math.Max(0, stops.Count - 1)];
            for (var position = 0; position < stops.Count - 1; position++)
            {
                hops[(int)service][position] = ModalHopMinutes(
                    ProfileOf(stops[position].Board, service), ProfileOf(stops[position + 1].Board, service));
            }
        }

        return hops;
    }

    private static int[] ProfileOf(MzkDepartureBoard board, MzkServiceType service)
    {
        var count = board.Count(service, MzkDayKind.Ordinary);
        var seconds = new int[count];
        for (var index = 0; index < count; index++)
        {
            seconds[index] = board.At(service, MzkDayKind.Ordinary, index);
        }

        return seconds;
    }

    /// <summary>
    /// Czas jazdy między sąsiednimi przystankami odczytany z wydrukowanych godzin: dla każdego odjazdu najbliższy
    /// odjazd na następnej stronie i moda różnicy. Wyrównanie po czasie, nie po indeksie (liczby odjazdów na
    /// stronach się różnią). Tylko sąsiednie strony: dla odległych najbliższy odjazd należałby do poprzedniego kursu.
    /// </summary>
    private static int ModalHopMinutes(int[] from, int[] to)
    {
        if (from.Length == 0 || to.Length == 0)
        {
            return -1;
        }

        var differences = new List<int>(from.Length);
        foreach (var departure in from)
        {
            var index = Array.BinarySearch(to, departure);
            var next = index >= 0 ? index : ~index;
            if (next < to.Length)
            {
                differences.Add((to[next] - departure) / 60);
            }
        }

        if (differences.Count < MinHopSamples)
        {
            return -1;
        }

        var mode = differences.GroupBy(minutes => minutes).OrderByDescending(group => group.Count()).ThenBy(group => group.Key).First();
        return mode.Count() * 2 < differences.Count || mode.Key > MaxHopMinutes ? -1 : mode.Key;
    }
}
