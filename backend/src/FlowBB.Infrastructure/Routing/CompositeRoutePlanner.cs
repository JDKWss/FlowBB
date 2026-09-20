using FlowBB.Application.Abstractions.Routing;
using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;
using Microsoft.Extensions.Logging;

namespace FlowBB.Infrastructure.Routing;

public sealed class CompositeRoutePlanner(
    DemoRoutePlanner demoPlanner,
    TimetableFallbackRoutePlanner transitPlanner,
    RoutingServiceRoutePlanner roadPlanner,
    RoutingServiceOptions options,
    ILogger<CompositeRoutePlanner> logger) : IRoutePlanner
{
    public async Task<RoutePlan> PlanAsync(RouteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Mode == TransportMode.PublicTransport)
        {
            // Rozklad MZK z kontrolowanym fallbackiem do planera demonstracyjnego (logika w TimetableFallbackRoutePlanner).
            return await transitPlanner.PlanAsync(request, cancellationToken);
        }

        try
        {
            return await roadPlanner.PlanAsync(request, cancellationToken);
        }
        catch (RoutingServiceException error) when (options.DemoFallbackEnabled && CanFallback(error.Failure))
        {
            logger.LogWarning(
                "Road routing unavailable with category {Failure}; using the deterministic demo planner.",
                error.Failure);
            return await demoPlanner.PlanAsync(request, cancellationToken);
        }
    }

    private static bool CanFallback(RoutingServiceFailure failure) => failure is
        RoutingServiceFailure.GraphNotReady or
        RoutingServiceFailure.TransportFailure or
        RoutingServiceFailure.Timeout;
}
