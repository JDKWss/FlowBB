using FlowBB.Application.Abstractions.Routing;
using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;

namespace FlowBB.Infrastructure.Routing;

public sealed class RoutingServiceRoutePlanner(RoutingServiceClient client) : IRoutePlanner
{
    public async Task<RoutePlan> PlanAsync(RouteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Mode == TransportMode.PublicTransport)
        {
            throw new RoutingServiceException(
                RoutingServiceFailure.UnsupportedMode,
                "Public transport is not supported by the road-routing planner.");
        }

        var outboundCalculation = await client.CalculateAsync(request, cancellationToken);
        // Powrot jedzie w druga strone: konstruktor RouteRequest przyjmuje (destination, origin), wiec punkty sa tu zamienione.
        var returnRequest = new RouteRequest(
            request.EventId,
            request.EventStartAt,
            request.EventEndAt,
            destination: request.Origin,
            origin: request.Destination,
            request.Mode);
        var returnCalculation = await client.CalculateAsync(returnRequest, cancellationToken);

        return new RoutePlan(
            PlannerSource.RoadRouting,
            BuildOutbound(request, outboundCalculation),
            [BuildReturn(request, returnCalculation)],
            returnGap: false);
    }

    private static JourneyOption BuildOutbound(RouteRequest request, RoadRouteCalculation calculation)
    {
        var duration = DurationMinutes(calculation.DurationSeconds);
        var arrival = RouteTiming.OutboundArrival(request.EventStartAt);
        return BuildJourney(request.Mode, calculation, duration, arrival.AddMinutes(-duration), arrival, outbound: true);
    }

    private static JourneyOption BuildReturn(RouteRequest request, RoadRouteCalculation calculation)
    {
        var duration = DurationMinutes(calculation.DurationSeconds);
        var departure = RouteTiming.FirstReturnDeparture(request.EventStartAt, request.EventEndAt);
        return BuildJourney(request.Mode, calculation, duration, departure, departure.AddMinutes(duration), outbound: false);
    }

    private static JourneyOption BuildJourney(
        TransportMode mode,
        RoadRouteCalculation calculation,
        int duration,
        DateTimeOffset departure,
        DateTimeOffset arrival,
        bool outbound)
    {
        var step = new RouteStep(StepType(mode), Instruction(mode, outbound), duration);
        var coordinates = calculation.Coordinates
            .Select(coordinate => new RouteCoordinate(coordinate[0], coordinate[1]))
            .ToArray();
        return new JourneyOption(
            duration,
            departure,
            arrival,
            [step],
            calculation.DistanceMeters,
            new RouteGeometry(coordinates));
    }

    private static int DurationMinutes(int seconds) => Math.Max(1, (int)Math.Ceiling(seconds / 60d));

    private static RouteStepType StepType(TransportMode mode) => mode switch
    {
        TransportMode.Walking => RouteStepType.Walk,
        TransportMode.Bike => RouteStepType.Bike,
        TransportMode.Car => RouteStepType.Car,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported road transport mode.")
    };

    private static string Instruction(TransportMode mode, bool outbound) => (mode, outbound) switch
    {
        (TransportMode.Walking, true) => "Walk along the calculated road route to the event.",
        (TransportMode.Walking, false) => "Walk along the calculated road route back to your starting point.",
        (TransportMode.Bike, true) => "Cycle along the calculated road route to the event.",
        (TransportMode.Bike, false) => "Cycle along the calculated road route back to your starting point.",
        (TransportMode.Car, true) => "Drive along the calculated road route to the event.",
        (TransportMode.Car, false) => "Drive along the calculated road route back to your starting point.",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported road transport mode.")
    };
}
