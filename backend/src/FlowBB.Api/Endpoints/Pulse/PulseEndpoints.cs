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
        group.MapGet("/events/{eventId:guid}", GetEventPulseAsync).WithName("getEventPulse");
        group.MapGet("/hexagons", GetHexagonsAsync).WithName("getPulseHexagons");

        return app;
    }

    private static async Task<IResult> GetSummaryAsync(GetPulseSummaryHandler handler, CancellationToken cancellationToken)
    {
        var summary = await handler.HandleAsync(cancellationToken);
        return TypedResults.Ok(summary.ToResponse());
    }

    private static async Task<IResult> GetEventPulseAsync(
        Guid eventId, GetEventPulseHandler handler, CancellationToken cancellationToken)
    {
        if (eventId == Guid.Empty)
        {
            return EventNotFound();
        }

        var pulse = await handler.HandleAsync(eventId, cancellationToken);
        return pulse is null ? EventNotFound() : TypedResults.Ok(pulse.ToResponse());
    }

    private static async Task<IResult> GetHexagonsAsync(
        Guid? eventId, GetPulseHexagonsHandler handler, CancellationToken cancellationToken)
    {
        if (eventId is null || eventId == Guid.Empty)
        {
            return TypedResults.Problem(title: "Query parameter 'eventId' is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var cells = await handler.HandleAsync(eventId.Value, cancellationToken);
        return cells is null
            ? EventNotFound()
            : TypedResults.Json(cells.ToFeatureCollection(), contentType: GeoJsonContentType);
    }

    private static IResult EventNotFound() =>
        TypedResults.Problem(title: "Event not found.", statusCode: StatusCodes.Status404NotFound);
}
