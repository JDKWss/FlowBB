using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Abstractions.Routing;
using FlowBB.Application.Pulse;
using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;

namespace FlowBB.Application.Routing.GetEventRoute;

public enum GetEventRouteStatus
{
    Ok,
    EventNotFound,
    AttendanceNotFound,
    InvalidTransportMode
}

public sealed record GetEventRouteResult(GetEventRouteStatus Status, RoutePlan? Plan);

public sealed class GetEventRouteHandler(
    IEventLookup events,
    IAttendanceOriginLookup origins,
    IRoutePlanner planner)
{
    public async Task<GetEventRouteResult> HandleAsync(
        Guid eventId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("Event id and user id cannot be empty GUIDs.");
        }

        var found = await events.FindByIdAsync(eventId, cancellationToken);
        if (found is null)
        {
            return new GetEventRouteResult(GetEventRouteStatus.EventNotFound, null);
        }

        var attendance = await origins.FindAsync(eventId, userId, cancellationToken);
        if (attendance is null)
        {
            return new GetEventRouteResult(GetEventRouteStatus.AttendanceNotFound, null);
        }

        // Unknown oznacza brakujacy lub niepoprawny tryb, wiec nie jest planowany jak komunikacja miejska.
        if (attendance.Mode == TransportMode.Unknown)
        {
            return new GetEventRouteResult(GetEventRouteStatus.InvalidTransportMode, null);
        }

        var request = new RouteRequest(
            eventId, found.StartAt, found.EndAt, found.Location, attendance.Origin, attendance.Mode);
        var plan = await planner.PlanAsync(request, cancellationToken);
        return new GetEventRouteResult(GetEventRouteStatus.Ok, ApplyReturnGap(plan, found.EndAt, attendance.Mode));
    }

    // Luka powrotowa jest regula demo (DemoReturnGapPolicy), niezalezna od planera: bez powrotow i z returnGap.
    private static RoutePlan ApplyReturnGap(RoutePlan plan, DateTimeOffset? endAt, TransportMode mode) =>
        DemoReturnGapPolicy.HasReturnGap(endAt, mode)
            ? new RoutePlan(plan.Source, plan.Outbound, [], returnGap: true)
            : plan;
}
