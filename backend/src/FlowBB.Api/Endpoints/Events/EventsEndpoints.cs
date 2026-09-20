using System.Globalization;
using FlowBB.Api.Endpoints;
using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Events;
using FlowBB.Application.Events.GetEvent;
using FlowBB.Application.Events.GetEvents;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlowBB.Api.Endpoints.Events;

public static class EventsEndpoints
{
    public static IServiceCollection AddEventsModule(this IServiceCollection services)
    {
        services.AddScoped<GetEventsHandler>();
        services.AddScoped<GetEventHandler>();
        services.TryAddScoped<IEventLookup, EventLookup>();
        return services;
    }

    public static IEndpointRouteBuilder MapEventsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events").WithTags("Events");

        group.MapGet("/", GetEventsAsync).WithName("getEvents");
        group.MapGet("/{eventId}", GetEventAsync).WithName("getEventById");

        return app;
    }

    private static async Task<IResult> GetEventsAsync(
        string? from, string? to, GetEventsHandler handler, CancellationToken cancellationToken)
    {
        if (!TryParseInstant(from, out var fromValue) || !TryParseInstant(to, out var toValue))
        {
            return ApiProblems.BadRequest("Query parameters 'from' and 'to' must be ISO 8601 date-times.");
        }

        var query = new GetEventsQuery(fromValue, toValue);
        if (!query.HasValidRange())
        {
            return ApiProblems.BadRequest("Query parameter 'from' cannot be later than 'to'.");
        }

        var events = await handler.HandleAsync(query, cancellationToken);
        return TypedResults.Ok(events.Select(item => item.ToSummaryResponse()).ToList());
    }

    // Nieparsowalny lub pusty id to 400, a poprawny, lecz nieznany id to 404 (getEventById w contracts/openapi.yaml).
    private static async Task<IResult> GetEventAsync(
        string eventId, GetEventHandler handler, CancellationToken cancellationToken)
    {
        if (!RouteIds.TryParse(eventId, out var id))
        {
            return ApiProblems.BadRequest("Path parameter 'eventId' must be a non-empty UUID.");
        }

        var details = await handler.HandleAsync(id, cancellationToken);
        return details is null ? ApiProblems.NotFound("Event not found.") : TypedResults.Ok(details.ToResponse());
    }

    private static bool TryParseInstant(string? raw, out DateTimeOffset? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }
}
