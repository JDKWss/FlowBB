using FlowBB.Api.Endpoints;
using FlowBB.Application.Pulse.GetActivityMap;
using FlowBB.Application.Pulse.GetEventPulse;
using FlowBB.Application.Pulse.GetPulseHexagons;
using FlowBB.Application.Pulse.GetPulseSummary;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlowBB.Api.Endpoints.Pulse;

public static class PulseEndpoints
{
    private const string GeoJsonContentType = "application/geo+json";

    public static IServiceCollection AddPulseModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<GetActivityMapHandler>();
        services.AddScoped<GetPulseHexagonsHandler>();
        services.AddScoped<GetEventPulseHandler>();
        services.AddScoped<GetPulseSummaryHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapPulseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pulse").WithTags("Pulse");

        group.MapGet("/summary", GetSummaryAsync).WithName("getPulseSummary");
        group.MapGet("/events/{eventId}", GetEventPulseAsync).WithName("getEventPulse");
        group.MapGet("/hexagons", GetHexagonsAsync).WithName("getPulseHexagons");

        return app;
    }

    private static async Task<IResult> GetSummaryAsync(GetPulseSummaryHandler handler, CancellationToken cancellationToken)
    {
        var summary = await handler.HandleAsync(cancellationToken);
        return TypedResults.Ok(summary.ToResponse());
    }

    private static async Task<IResult> GetEventPulseAsync(
        string eventId, GetEventPulseHandler handler, CancellationToken cancellationToken)
    {
        if (!RouteIds.TryParse(eventId, out var eventGuid))
        {
            return ApiProblems.BadRequest("Path parameter 'eventId' must be a non-empty UUID.");
        }

        var pulse = await handler.HandleAsync(eventGuid, cancellationToken);
        return pulse is null ? ApiProblems.NotFound("Event not found.") : TypedResults.Ok(pulse.ToResponse());
    }

    private static async Task<IResult> GetHexagonsAsync(
        string? eventId, GetPulseHexagonsHandler handler, CancellationToken cancellationToken)
    {
        if (!RouteIds.TryParse(eventId, out var eventGuid))
        {
            return ApiProblems.BadRequest("Query parameter 'eventId' is required and must be a non-empty UUID.");
        }

        var cells = await handler.HandleAsync(eventGuid, cancellationToken);
        return cells is null
            ? ApiProblems.NotFound("Event not found.")
            : TypedResults.Json(cells.ToFeatureCollection(), contentType: GeoJsonContentType);
    }
}
