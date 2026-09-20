using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;
using Microsoft.Extensions.Logging;

namespace FlowBB.Infrastructure.Routing.Mzk;

internal enum MzkPlanningFailure
{
    UnsupportedMode,
    TimetableUnavailable,
    DateOutsideCalendar,
    NoStopNearOrigin,
    NoStopNearDestination,
    NoOutboundConnection
}

/// <summary>
/// Wynik planowania. Brak połączenia jest oczekiwanym wynikiem przy pięciu liniach w zbiorze, a nie wyjątkiem.
/// <see cref="Detail"/> nigdy nie zawiera współrzędnych punktu startu ani celu: to dane wewnętrzne backendu.
/// </summary>
internal sealed record MzkPlanningOutcome(RoutePlan? Plan, MzkPlanningFailure? Failure, string? Detail)
{
    internal static MzkPlanningOutcome Planned(RoutePlan plan) => new(plan, null, null);

    internal static MzkPlanningOutcome Failed(MzkPlanningFailure failure, string detail) => new(null, failure, detail);
}

/// <summary>Leniwie ładuje rozkład raz na proces. Konstruktor z gotowym rozkładem służy testom.</summary>
public sealed class MzkTimetableProvider
{
    private readonly Lazy<MzkTimetable> _timetable;

    public MzkTimetableProvider(ILogger<MzkTimetableProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _timetable = new Lazy<MzkTimetable>(() => MzkTimetableLoader.LoadOrEmpty(logger), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    internal MzkTimetableProvider(MzkTimetable timetable)
    {
        _timetable = new Lazy<MzkTimetable>(() => timetable);
    }

    internal MzkTimetable Timetable => _timetable.Value;
}

/// <summary>
/// Planer komunikacji miejskiej z rozkładu MZK. Wynik zależy wyłącznie od <see cref="RouteRequest"/> i danych
/// rozkładu: bez zegara, sieci i losowości, więc ta sama deklaracja daje zawsze tę samą trasę.
/// Założenia (ZALOZENIE = nie potwierdzona praktyka MZK): kursy bez przesiadek (Z-9); odjazd należy do doby, nad którą
/// jest wydrukowany (Z-1); dojście pieszo jest szacunkiem w linii prostej (Z-8).
/// </summary>
public sealed class MzkTimetableRoutePlanner(MzkTimetableProvider provider)
{
    /// <summary>Promień dojścia do przystanku: ok. 10 min pieszo, tyle samo co komórka heksagonalna PULSE (AGENTS.md §8).</summary>
    internal const double MaxAccessKilometers = 0.8;

    /// <summary>Czekanie po wydarzeniu dłuższe niż ten próg oznacza brak dogodnego powrotu.</summary>
    internal static readonly TimeSpan ReturnGapThreshold = TimeSpan.FromMinutes(45);

    internal const int ReturnSearchSeconds = 6 * 60 * 60;
    internal const int MaxReturnOptions = 2;

    internal MzkPlanningOutcome Plan(RouteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Mode != TransportMode.PublicTransport)
        {
            return MzkPlanningOutcome.Failed(MzkPlanningFailure.UnsupportedMode, $"Mode {request.Mode} is not public transport.");
        }

        var timetable = provider.Timetable;
        if (!timetable.IsUsable)
        {
            return MzkPlanningOutcome.Failed(MzkPlanningFailure.TimetableUnavailable, "The timetable has no patterns, stop coordinates or calendar.");
        }

        var arriveAt = RouteTiming.OutboundArrival(request.EventStartAt);
        var (day, arriveBySecond) = MzkLocalTime.ToLocal(arriveAt);
        if (!timetable.Calendar.TryGetService(day, out _))
        {
            return MzkPlanningOutcome.Failed(
                MzkPlanningFailure.DateOutsideCalendar,
                $"{day:yyyy-MM-dd} is outside the calendar {timetable.Calendar.FirstDay:yyyy-MM-dd}..{timetable.Calendar.LastDay:yyyy-MM-dd}.");
        }

        return PlanWithinCalendar(request, timetable, day, arriveBySecond);
    }

    private static MzkPlanningOutcome PlanWithinCalendar(RouteRequest request, MzkTimetable timetable, DateOnly day, int arriveBySecond)
    {
        var nearOrigin = timetable.Stops.Within(request.Origin, MaxAccessKilometers);
        if (nearOrigin.Count == 0)
        {
            return MzkPlanningOutcome.Failed(MzkPlanningFailure.NoStopNearOrigin, $"No stop within {MaxAccessKilometers * 1000:0} m of the origin.");
        }

        var nearEvent = timetable.Stops.Within(request.Destination, MaxAccessKilometers);
        if (nearEvent.Count == 0)
        {
            return MzkPlanningOutcome.Failed(MzkPlanningFailure.NoStopNearDestination, $"No stop within {MaxAccessKilometers * 1000:0} m of the event.");
        }

        var outbound = MzkConnectionFinder.FindOutbound(timetable, nearOrigin, nearEvent, day, arriveBySecond);
        if (outbound is null)
        {
            return MzkPlanningOutcome.Failed(MzkPlanningFailure.NoOutboundConnection, "No direct connection reaches the event in time.");
        }

        return MzkPlanningOutcome.Planned(BuildPlan(request, timetable, outbound, nearOrigin, nearEvent));
    }

    private static RoutePlan BuildPlan(
        RouteRequest request,
        MzkTimetable timetable,
        MzkConnection outbound,
        IReadOnlyList<MzkNearbyStop> nearOrigin,
        IReadOnlyList<MzkNearbyStop> nearEvent)
    {
        var readyAt = RouteTiming.FirstReturnDeparture(request.EventStartAt, request.EventEndAt);
        var returns = MzkConnectionFinder.FindReturns(timetable, nearEvent, nearOrigin, readyAt, ReturnSearchSeconds, MaxReturnOptions);
        var returnGap = returns.Count == 0 || returns[0].BoardAt - readyAt > ReturnGapThreshold;
        return new RoutePlan(
            PlannerSource.MzkTimetable,
            MzkJourneyBuilder.Outbound(outbound),
            [.. returns.Select(connection => MzkJourneyBuilder.Return(connection, readyAt))],
            returnGap);
    }
}
