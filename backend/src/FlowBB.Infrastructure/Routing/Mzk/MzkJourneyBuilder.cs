using System.Globalization;
using FlowBB.Domain.Routing;

namespace FlowBB.Infrastructure.Routing.Mzk;

/// <summary>
/// Zamienia kurs z rozkładu w <see cref="JourneyOption"/>. Godziny odjazdu i przyjazdu autobusu są wydrukowane w
/// rozkładzie; jedynym szacunkiem jest dojście pieszo i instrukcje oznaczają to wprost. Teksty są bez polskich
/// znaków jak w <c>DemoRoutePlanner</c>, a nazwy przystanków przechodzą dosłownie, bo pasażer szuka ich na słupku.
/// </summary>
internal static class MzkJourneyBuilder
{
    /// <summary>Zapas na dojście do słupka przed odjazdem, żeby plan nie wymagał wejścia do autobusu w biegu.</summary>
    internal const int BoardingBufferMinutes = 2;

    private const int StopNameDisplayLength = 60;
    private const string WalkEstimate = "(szacunek, linia prosta)";

    internal static JourneyOption Outbound(MzkConnection connection)
    {
        var leaveAt = connection.BoardAt.AddMinutes(-(connection.WalkToBoardMinutes + BoardingBufferMinutes));
        var steps = Steps(
            connection,
            waitMinutes: BoardingBufferMinutes,
            walkToStop: $"Idz {connection.WalkToBoardMinutes} min do przystanku {Name(connection.Pair.Board)} {WalkEstimate}.",
            walkFromStop: $"Idz {connection.WalkFromAlightMinutes} min na miejsce wydarzenia {WalkEstimate}.");
        return Journey(connection, leaveAt, steps);
    }

    /// <summary>Powrót zaczyna się, gdy mieszkaniec jest gotowy do wyjścia, a czekanie na słupku jest nazwane liczbą minut.</summary>
    internal static JourneyOption Return(MzkConnection connection, DateTimeOffset readyAt)
    {
        var minutesUntilBoarding = (int)Math.Floor((connection.BoardAt - readyAt).TotalMinutes);
        var wait = Math.Max(0, minutesUntilBoarding - connection.WalkToBoardMinutes);
        var leaveAt = connection.BoardAt.AddMinutes(-(connection.WalkToBoardMinutes + wait));
        var steps = Steps(
            connection,
            waitMinutes: wait,
            walkToStop: $"Idz {connection.WalkToBoardMinutes} min do przystanku {Name(connection.Pair.Board)} {WalkEstimate}.",
            walkFromStop: $"Idz {connection.WalkFromAlightMinutes} min do punktu startu {WalkEstimate}.");
        return Journey(connection, leaveAt, steps);
    }

    private static JourneyOption Journey(MzkConnection connection, DateTimeOffset leaveAt, List<RouteStep> steps)
    {
        var arriveAt = connection.AlightAt.AddMinutes(connection.WalkFromAlightMinutes);
        var stops = new List<RouteStop>
        {
            new(connection.Pair.Board.Name, connection.Pair.Board.Location),
            new(connection.Pair.Alight.Name, connection.Pair.Alight.Location)
        };
        return new JourneyOption(steps.Sum(step => step.DurationMinutes), leaveAt, arriveAt, steps, stops: stops);
    }

    private static List<RouteStep> Steps(MzkConnection connection, int waitMinutes, string walkToStop, string walkFromStop)
    {
        var steps = new List<RouteStep> { new(RouteStepType.Walk, walkToStop, connection.WalkToBoardMinutes) };
        if (waitMinutes >= 1)
        {
            steps.Add(new RouteStep(
                RouteStepType.Wait,
                $"Poczekaj {waitMinutes} min na linie {connection.Line} (rozklad MZK).",
                waitMinutes,
                connection.Line));
        }

        steps.Add(new RouteStep(RouteStepType.Transit, TransitInstruction(connection), connection.RideMinutes, connection.Line));
        steps.Add(new RouteStep(RouteStepType.Walk, walkFromStop, connection.WalkFromAlightMinutes));
        return steps;
    }

    private static string TransitInstruction(MzkConnection connection) =>
        $"Linia {connection.Line}, rozklad MZK: {Clock(connection.Day, connection.BoardSecond)} {Name(connection.Pair.Board)} -> " +
        $"{Clock(connection.Day, connection.AlightSecond)} {Name(connection.Pair.Alight)}.";

    // Ta sama godzina, którą mapper pokaże w departureAt: obie wartości pochodzą z tej samej chwili w Europe/Warsaw.
    private static string Clock(DateOnly day, int secondOfDay) =>
        MzkLocalTime.ToInstant(day, secondOfDay).ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string Name(MzkNearbyStop stop) =>
        stop.Name.Length <= StopNameDisplayLength ? stop.Name : string.Concat(stop.Name.AsSpan(0, StopNameDisplayLength - 3), "...");
}
