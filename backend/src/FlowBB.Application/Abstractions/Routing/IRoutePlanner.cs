using FlowBB.Domain.Routing;

namespace FlowBB.Application.Abstractions.Routing;

/// <summary>
/// Port planera trasy. Kod domenowy i endpointy zawsze korzystaja z tego portu, nigdy bezposrednio z OpenTripPlanner.
/// </summary>
public interface IRoutePlanner
{
    Task<RoutePlan> PlanAsync(RouteRequest request, CancellationToken cancellationToken = default);
}
