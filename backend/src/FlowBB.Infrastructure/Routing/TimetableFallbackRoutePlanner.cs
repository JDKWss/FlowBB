using FlowBB.Application.Abstractions.Routing;
using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;
using FlowBB.Infrastructure.Routing.Mzk;
using Microsoft.Extensions.Logging;

namespace FlowBB.Infrastructure.Routing;

/// <summary>
/// Komunikacja miejska planowana z rozkładu MZK, a gdy rozkład nie umie zaplanować trasy (brak przystanku w zasięgu,
/// data poza kalendarzem, brak połączenia bez przesiadki) - kontrolowany fallback do <see cref="DemoRoutePlanner"/>.
/// Pozostałe tryby przechodzą do planera demonstracyjnego bez zmian. Klasa jest wspólna dla trybów Demo i RoadRouting,
/// żeby planer rozkładowy działał także w domyślnej konfiguracji, w której nie ma <see cref="CompositeRoutePlanner"/>.
/// </summary>
public sealed class TimetableFallbackRoutePlanner(
    MzkTimetableRoutePlanner timetablePlanner,
    DemoRoutePlanner demoPlanner,
    ILogger<TimetableFallbackRoutePlanner> logger) : IRoutePlanner
{
    public async Task<RoutePlan> PlanAsync(RouteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Mode != TransportMode.PublicTransport)
        {
            return await demoPlanner.PlanAsync(request, cancellationToken);
        }

        var outcome = timetablePlanner.Plan(request);
        if (outcome.Plan is not null)
        {
            return outcome.Plan;
        }

        LogDegradation(request.EventId, outcome);
        return await demoPlanner.PlanAsync(request, cancellationToken);
    }

    private void LogDegradation(Guid eventId, MzkPlanningOutcome outcome)
    {
        if (outcome.Failure == MzkPlanningFailure.TimetableUnavailable)
        {
            logger.LogWarning(
                "MZK timetable is unavailable ({Detail}); using the demo planner for event {EventId}.", outcome.Detail, eventId);
            return;
        }

        // Brak przystanku w zasięgu albo data poza kalendarzem to normalny wynik dla pięciu linii, nie awaria.
        logger.LogInformation(
            "MZK timetable cannot plan event {EventId}: {Failure} ({Detail}); using the demo planner.",
            eventId,
            outcome.Failure,
            outcome.Detail);
    }
}
