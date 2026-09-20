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
        if (!TryParseId(eventId, out var eventGuid))
        {
            return BadRequest("Path parameter 'eventId' must be a non-empty UUID.");
        }

        if (!TryParseId(userId, out var userGuid))
        {
            return BadRequest("Query parameter 'userId' is required and must be a non-empty UUID.");
        }

        var result = await handler.HandleAsync(eventGuid, userGuid, cancellationToken);
        return result.Status switch
        {
            GetEventRouteStatus.EventNotFound => NotFound("Event not found."),
            GetEventRouteStatus.AttendanceNotFound => NotFound("The user has not declared attendance for this event."),
            GetEventRouteStatus.InvalidTransportMode =>
                BadRequest("The declared transport mode is missing or invalid, so no route can be planned."),
            _ => TypedResults.Ok(result.Plan!.ToResponse(eventGuid, userGuid))
        };
    }

    private static bool TryParseId(string? raw, out Guid id) => Guid.TryParse(raw, out id) && id != Guid.Empty;

    private static IResult BadRequest(string title) =>
        TypedResults.Problem(title: title, statusCode: StatusCodes.Status400BadRequest);

    private static IResult NotFound(string title) =>
        TypedResults.Problem(title: title, statusCode: StatusCodes.Status404NotFound);
}
