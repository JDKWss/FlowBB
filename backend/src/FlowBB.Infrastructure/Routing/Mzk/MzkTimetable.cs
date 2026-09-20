using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;

namespace FlowBB.Infrastructure.Routing.Mzk;

/// <summary>
/// Posortowane sekundy odjazdów jednej strony PDF (przystanek jednego kierunku), po jednej tablicy na parę
/// (typ rozkładu, rodzaj doby). Zjazdy do zajezdni i odjazdy wyłączone flagami są odfiltrowane przy budowie,
/// więc zapytania są czystym wyszukiwaniem binarnym.
/// </summary>
internal sealed class MzkDepartureBoard
{
    private const int DayKindCount = 2;

    private readonly int[][] _byProfile;

    internal MzkDepartureBoard(int[][] byProfile)
    {
        if (byProfile.Length != Enum.GetValues<MzkServiceType>().Length * DayKindCount)
        {
            throw new ArgumentException("A board needs one departure array per service type and day kind.", nameof(byProfile));
        }

        _byProfile = byProfile;
    }

    internal static int ProfileIndex(MzkServiceType service, MzkDayKind kind) => ((int)service * DayKindCount) + (int)kind;

    internal int Count(MzkServiceType service, MzkDayKind kind) => _byProfile[ProfileIndex(service, kind)].Length;

    internal int At(MzkServiceType service, MzkDayKind kind, int index) => _byProfile[ProfileIndex(service, kind)][index];

    /// <summary>Indeks pierwszego odjazdu nie wcześniejszego niż podana sekunda albo -1.</summary>
    internal int IndexOfFirstAtOrAfter(MzkServiceType service, MzkDayKind kind, int second)
    {
        var departures = _byProfile[ProfileIndex(service, kind)];
        var index = Array.BinarySearch(departures, second);
        var first = index >= 0 ? index : ~index;
        return first < departures.Length ? first : -1;
    }

    /// <summary>Indeks ostatniego odjazdu nie późniejszego niż podana sekunda albo -1.</summary>
    internal int IndexOfLastAtOrBefore(MzkServiceType service, MzkDayKind kind, int second)
    {
        var departures = _byProfile[ProfileIndex(service, kind)];
        var index = Array.BinarySearch(departures, second);
        return index >= 0 ? index : ~index - 1;
    }
}

internal sealed record MzkPatternStop(string Name, MzkDepartureBoard Board);

/// <summary>
/// Jeden kierunek jednej linii: przystanki w kolejności trasy (kolejność stron w PDF). Czas przejazdu między
/// sąsiednimi przystankami odczytany z wydrukowanych godzin jest prywatny dla wzorca: łączenie nieprzylegających
/// stron dawałoby aliasing (najbliższy odjazd po czasie należałby do poprzedniego kursu).
/// </summary>
internal sealed class MzkRoutePattern
{
    /// <summary>Tolerancja ponad zmierzony czas odcinka, w której odjazd na następnym przystanku nadal jest tym samym kursem.</summary>
    internal const int HopToleranceMinutes = 2;

    private static readonly int[] NoPositions = [];

    private readonly int[][] _hopMinutes;
    private readonly Dictionary<string, List<int>> _positionsByName;

    internal MzkRoutePattern(string line, string direction, IReadOnlyList<MzkPatternStop> stops, int[][] hopMinutes)
    {
        Line = line;
        Direction = direction;
        Stops = stops;
        _hopMinutes = hopMinutes;
        _positionsByName = [];
        for (var position = 0; position < stops.Count; position++)
        {
            if (!_positionsByName.TryGetValue(stops[position].Name, out var positions))
            {
                positions = [];
                _positionsByName[stops[position].Name] = positions;
            }

            positions.Add(position);
        }
    }

    internal string Line { get; }

    internal string Direction { get; }

    internal IReadOnlyList<MzkPatternStop> Stops { get; }

    /// <summary>Pozycje przystanku o tej nazwie na trasie; pętla może mieć ten sam przystanek dwa razy.</summary>
    internal IReadOnlyList<int> PositionsOf(string stopName) =>
        _positionsByName.TryGetValue(stopName, out var positions) ? positions : NoPositions;

    /// <summary>Czas jazdy z pozycji na następną w minutach albo -1, gdy odcinek jest niepewny.</summary>
    internal int HopMinutes(MzkServiceType service, int fromPosition) => _hopMinutes[(int)service][fromPosition];

    /// <summary>
    /// Idzie kursem po wydrukowanych godzinach od przystanku wsiadania do wysiadania. Zwraca sekundę odjazdu
    /// z przystanku wysiadania albo -1, gdy kurs tam nie dojeżdża (kurs skrócony, niepewny odcinek, koniec doby).
    /// </summary>
    internal int FollowTrip(MzkServiceType service, MzkDayKind kind, int boardPosition, int alightPosition, int boardSecond)
    {
        var current = boardSecond;
        for (var position = boardPosition; position < alightPosition; position++)
        {
            var hop = HopMinutes(service, position);
            if (hop < 0)
            {
                return -1;
            }

            var board = Stops[position + 1].Board;
            var index = board.IndexOfFirstAtOrAfter(service, kind, current);
            if (index < 0)
            {
                return -1;
            }

            var next = board.At(service, kind, index);
            if (next - current > (hop + HopToleranceMinutes) * 60)
            {
                return -1;
            }

            current = next;
        }

        return current;
    }
}

internal readonly record struct MzkNearbyStop(string Name, GeoPoint Location, double DistanceKilometers);

/// <summary>Przystanki ze współrzędnymi. Przy kilkudziesięciu przystankach liniowy skan jest szybszy niż indeks przestrzenny.</summary>
internal sealed class MzkStopIndex
{
    private readonly List<(string Name, GeoPoint Location)> _stops;

    internal MzkStopIndex(IEnumerable<KeyValuePair<string, GeoPoint>> stops)
    {
        _stops = [.. stops.Select(stop => (stop.Key, stop.Value)).OrderBy(stop => stop.Key, StringComparer.Ordinal)];
    }

    internal static MzkStopIndex Empty { get; } = new([]);

    internal int Count => _stops.Count;

    /// <summary>Przystanki w promieniu, od najbliższego; remis rozstrzyga nazwa, żeby wynik był deterministyczny.</summary>
    internal IReadOnlyList<MzkNearbyStop> Within(GeoPoint point, double maxKilometers) =>
        [.. _stops
            .Select(stop => new MzkNearbyStop(stop.Name, stop.Location, GeoDistance.KilometersBetween(point, stop.Location)))
            .Where(stop => stop.DistanceKilometers <= maxKilometers)
            .OrderBy(stop => stop.DistanceKilometers)
            .ThenBy(stop => stop.Name, StringComparer.Ordinal)];
}

/// <summary>Niemutowalny rozkład MZK: wzorce tras, przystanki ze współrzędnymi i kalendarz.</summary>
internal sealed class MzkTimetable
{
    internal MzkTimetable(IReadOnlyList<MzkRoutePattern> patterns, MzkStopIndex stops, MzkServiceCalendar calendar)
    {
        Patterns = patterns;
        Stops = stops;
        Calendar = calendar;
    }

    internal static MzkTimetable Empty { get; } = new([], MzkStopIndex.Empty, MzkServiceCalendar.Empty);

    internal IReadOnlyList<MzkRoutePattern> Patterns { get; }

    internal MzkStopIndex Stops { get; }

    internal MzkServiceCalendar Calendar { get; }

    /// <summary>Rozkład bez wzorców, przystanków ze współrzędnymi albo kalendarza nie pozwala niczego zaplanować.</summary>
    internal bool IsUsable => Patterns.Count > 0 && Stops.Count > 0 && Calendar.HasDays;
}
