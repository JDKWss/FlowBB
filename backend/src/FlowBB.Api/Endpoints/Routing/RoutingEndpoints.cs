using FlowBB.Api.Endpoints;
using FlowBB.Application.Abstractions.Routing;
using FlowBB.Application.Routing.GetEventRoute;
using FlowBB.Infrastructure.Routing;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlowBB.Api.Endpoints.Routing;

public static class RoutingEndpoints
{
    public static IServiceCollection AddRoutingModule(this IServiceCollection services)
    {
        services.TryAddSingleton<IRoutePlanner, DemoRoutePlanner>();
        services.AddScoped<GetEventRouteHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapRoutingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events/{eventId}/route", GetEventRouteAsync)
            .WithTags("Routing")
            .WithName("getEventRoute");

        return app;
    }

    private static async Task<IResult> GetEventRouteAsync(
        string eventId, string? userId, GetEventRouteHandler handler, CancellationToken cancellationToken)
    {
        if (!RouteIds.TryParse(eventId, out var eventGuid))
        {
            return ApiProblems.BadRequest("Path parameter 'eventId' must be a non-empty UUID.");
        }

        if (!RouteIds.TryParse(userId, out var userGuid))
        {
            return ApiProblems.BadRequest("Query parameter 'userId' is required and must be a non-empty UUID.");
        }

        var result = await handler.HandleAsync(eventGuid, userGuid, cancellationToken);
        return result.Status switch
        {
            GetEventRouteStatus.EventNotFound => ApiProblems.NotFound("Event not found."),
            GetEventRouteStatus.AttendanceNotFound => ApiProblems.NotFound("The user has not declared attendance for this event."),
            GetEventRouteStatus.InvalidTransportMode =>
                ApiProblems.BadRequest("The declared transport mode is missing or invalid, so no route can be planned."),
            _ => TypedResults.Ok(result.Plan!.ToResponse(eventGuid, userGuid))
        };
    }
}
