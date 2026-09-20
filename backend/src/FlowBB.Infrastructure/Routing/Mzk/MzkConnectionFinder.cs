namespace FlowBB.Infrastructure.Routing.Mzk;

/// <summary>Para przystanków na jednym wzorcu trasy, w kolejności jazdy: wsiadanie przed wysiadaniem.</summary>
internal readonly record struct MzkStopPair(
    MzkRoutePattern Pattern, MzkNearbyStop Board, int BoardPosition, MzkNearbyStop Alight, int AlightPosition);

/// <summary>
/// Kurs bez przesiadek. Sekundy są sekundami doby <see cref="Day"/>; oba końce (odjazd i przyjazd na przystanek
/// wysiadania) są wydrukowanymi godzinami rozkładu.
/// </summary>
internal sealed record MzkConnection(MzkStopPair Pair, DateOnly Day, int BoardSecond, int AlightSecond)
{
    internal string Line => Pair.Pattern.Line;

    internal string Direction => Pair.Pattern.Direction;

    internal int WalkToBoardMinutes => MzkWalking.Minutes(Pair.Board.DistanceKilometers);

    internal int WalkFromAlightMinutes => MzkWalking.Minutes(Pair.Alight.DistanceKilometers);

    internal int RideMinutes => (AlightSecond - BoardSecond) / 60;

    internal DateTimeOffset BoardAt => MzkLocalTime.ToInstant(Day, BoardSecond);

    internal DateTimeOffset AlightAt => MzkLocalTime.ToInstant(Day, AlightSecond);
}

/// <summary>Dojście pieszo jest jedynym szacunkiem w planie: linia prosta z współczynnikiem objazdu.</summary>
internal static class MzkWalking
{
    private const double WalkSpeedKmh = 4.8;
    private const double DetourFactor = 1.3;

    internal static int Minutes(double straightLineKilometers) =>
        Math.Max(1, (int)Math.Ceiling(straightLineKilometers * DetourFactor / WalkSpeedKmh * 60));
}

/// <summary>Dobór połączenia z rozkładu. Wszystkie porównania mają pełny porządek, więc wynik jest deterministyczny.</summary>
internal static class MzkConnectionFinder
{
    /// <summary>Ile kolejnych kursów sprawdzamy, zanim uznamy, że linia nie obsługuje tej pary przystanków.</summary>
    private const int MaxBoardingProbes = 8;

    /// <summary>Najpóźniejszy odjazd, który dowozi pasażera do przystanku wysiadania i pozwala dojść do celu przed terminem.</summary>
    internal static MzkConnection? FindOutbound(
        MzkTimetable timetable,
        IReadOnlyList<MzkNearbyStop> boards,
        IReadOnlyList<MzkNearbyStop> alights,
        DateOnly day,
        int arriveBySecond)
    {
        if (!timetable.Calendar.TryGetService(day, out var service))
        {
            return null;
        }

        return timetable.Patterns
            .SelectMany(pattern => Pairs(pattern, boards, alights))
            .Select(pair => LatestReaching(pair, day, service, arriveBySecond))
            .OfType<MzkConnection>()
            .OrderBy(connection => connection, OutboundOrder)
            .FirstOrDefault();
    }

    /// <summary>Do <paramref name="maxOptions"/> najwcześniejszych powrotów; jeden wzorzec daje najlepszą parę przystanków.</summary>
    internal static List<MzkConnection> FindReturns(
        MzkTimetable timetable,
        IReadOnlyList<MzkNearbyStop> boards,
        IReadOnlyList<MzkNearbyStop> alights,
        DateTimeOffset readyAt,
        int searchSeconds,
        int maxOptions) =>
        [.. timetable.Patterns
            .Select(pattern => BestPairReturns(timetable, pattern, boards, alights, readyAt, searchSeconds, maxOptions))
            .SelectMany(connections => connections)
            .OrderBy(connection => connection, ReturnOrder)
            .Take(maxOptions)];

    private static IEnumerable<MzkStopPair> Pairs(
        MzkRoutePattern pattern, IReadOnlyList<MzkNearbyStop> boards, IReadOnlyList<MzkNearbyStop> alights) =>
        from board in boards
        from boardPosition in pattern.PositionsOf(board.Name)
        from alight in alights
        from alightPosition in pattern.PositionsOf(alight.Name)
        where alightPosition > boardPosition
        select new MzkStopPair(pattern, board, boardPosition, alight, alightPosition);

    private static MzkConnection? LatestReaching(MzkStopPair pair, DateOnly day, MzkServiceType service, int arriveBySecond)
    {
        var lastAlight = arriveBySecond - (MzkWalking.Minutes(pair.Alight.DistanceKilometers) * 60);
        var kind = MzkServiceCalendar.DayKindOf(day);
        var board = pair.Pattern.Stops[pair.BoardPosition].Board;
        var index = board.IndexOfLastAtOrBefore(service, kind, lastAlight);
        for (var probes = 0; index >= 0 && probes < MaxBoardingProbes; index--, probes++)
        {
            var boardSecond = board.At(service, kind, index);
            var alightSecond = pair.Pattern.FollowTrip(service, kind, pair.BoardPosition, pair.AlightPosition, boardSecond);
            if (alightSecond >= 0 && alightSecond <= lastAlight)
            {
                return new MzkConnection(pair, day, boardSecond, alightSecond);
            }
        }

        return null;
    }

    private static List<MzkConnection> BestPairReturns(
        MzkTimetable timetable,
        MzkRoutePattern pattern,
        IReadOnlyList<MzkNearbyStop> boards,
        IReadOnlyList<MzkNearbyStop> alights,
        DateTimeOffset readyAt,
        int searchSeconds,
        int maxOptions) =>
        Pairs(pattern, boards, alights)
            .Select(pair => EarliestAfter(timetable, pair, readyAt, searchSeconds, maxOptions))
            .Where(connections => connections.Count > 0)
            .OrderBy(connections => connections[0], ReturnOrder)
            .FirstOrDefault() ?? [];

    private static List<MzkConnection> EarliestAfter(
        MzkTimetable timetable, MzkStopPair pair, DateTimeOffset readyAt, int searchSeconds, int maxOptions)
    {
        var found = new List<MzkConnection>(maxOptions);
        var leavesStopAt = readyAt.AddMinutes(MzkWalking.Minutes(pair.Board.DistanceKilometers));
        var (day, second) = MzkLocalTime.ToLocal(leavesStopAt);
        foreach (var window in timetable.Calendar.WindowsFrom(day, second, searchSeconds))
        {
            AddWindowConnections(pair, window, found, maxOptions);
            if (found.Count >= maxOptions)
            {
                break;
            }
        }

        return found;
    }

    private static void AddWindowConnections(MzkStopPair pair, MzkServiceWindow window, List<MzkConnection> found, int maxOptions)
    {
        var kind = MzkServiceCalendar.DayKindOf(window.Day);
        var board = pair.Pattern.Stops[pair.BoardPosition].Board;
        var index = board.IndexOfFirstAtOrAfter(window.Service, kind, window.FromSecond);
        for (var probes = 0; index >= 0 && index < board.Count(window.Service, kind) && probes < MaxBoardingProbes; index++, probes++)
        {
            var boardSecond = board.At(window.Service, kind, index);
            if (boardSecond > window.ToSecond || found.Count >= maxOptions)
            {
                return;
            }

            var alightSecond = pair.Pattern.FollowTrip(window.Service, kind, pair.BoardPosition, pair.AlightPosition, boardSecond);
            if (alightSecond >= 0)
            {
                found.Add(new MzkConnection(pair, window.Day, boardSecond, alightSecond));
            }
        }
    }

    // Dojazd: jak najpóźniejszy odjazd, potem krótsza całość, mniej pieszo, i pełny porządek jako ostatnie rozstrzygnięcie.
    private static readonly Comparer<MzkConnection> OutboundOrder = Comparer<MzkConnection>.Create((x, y) =>
        Chain(
            y.BoardAt.CompareTo(x.BoardAt),
            TotalMinutes(x).CompareTo(TotalMinutes(y)),
            (x.WalkToBoardMinutes + x.WalkFromAlightMinutes).CompareTo(y.WalkToBoardMinutes + y.WalkFromAlightMinutes),
            Tiebreak(x, y)));

    // Powrót: jak najwcześniejszy odjazd.
    private static readonly Comparer<MzkConnection> ReturnOrder = Comparer<MzkConnection>.Create((x, y) =>
        Chain(
            x.BoardAt.CompareTo(y.BoardAt),
            TotalMinutes(x).CompareTo(TotalMinutes(y)),
            (x.WalkToBoardMinutes + x.WalkFromAlightMinutes).CompareTo(y.WalkToBoardMinutes + y.WalkFromAlightMinutes),
            Tiebreak(x, y)));

    private static int TotalMinutes(MzkConnection connection) =>
        connection.WalkToBoardMinutes + connection.RideMinutes + connection.WalkFromAlightMinutes;

    private static int Tiebreak(MzkConnection x, MzkConnection y)
    {
        var byLine = StringComparer.Ordinal.Compare(x.Line, y.Line);
        if (byLine != 0)
        {
            return byLine;
        }

        var byDirection = StringComparer.Ordinal.Compare(x.Direction, y.Direction);
        return byDirection != 0
            ? byDirection
            : Chain(x.Pair.BoardPosition.CompareTo(y.Pair.BoardPosition), x.Pair.AlightPosition.CompareTo(y.Pair.AlightPosition), 0, 0);
    }

    private static int Chain(int first, int second, int third, int fourth)
    {
        if (first != 0)
        {
            return first;
        }

        if (second != 0)
        {
            return second;
        }

        return third != 0 ? third : fourth;
    }
}
