using FlowBB.Api.Endpoints;
using FlowBB.Application.Abstractions.AirQuality;
using FlowBB.Application.AirQuality.GetEventAirQuality;
using FlowBB.Infrastructure.AirQuality;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlowBB.Api.Endpoints.AirQuality;

public static class AirQualityEndpoints
{
    public static IServiceCollection AddAirQualityModule(
        this IServiceCollection services,
        AirQualityPolicyOptions policy,
        Uri giosBaseAddress)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddSingleton(policy);
        services.TryAddSingleton<IAirQualityCache, MemoryAirQualityCache>();
        services.TryAddSingleton<IAirQualityFallbackProvider, DemoAirQualitySnapshotProvider>();
        services.AddScoped<GetEventAirQualityHandler>();
        services.AddHttpClient<IAirQualityProvider, GiosAirQualityProvider>(client =>
        {
            client.BaseAddress = giosBaseAddress;
            client.Timeout = policy.SourceTimeout;
        });
        return services;
    }

    public static IEndpointRouteBuilder MapAirQualityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events/{eventId}/air-quality", GetEventAirQualityAsync)
            .WithTags("AirQuality")
            .WithName("getEventAirQuality")
            .Produces<AirQualityResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
        return app;
    }

    private static async Task<IResult> GetEventAirQualityAsync(
        string eventId,
        GetEventAirQualityHandler handler,
        ILogger<GetEventAirQualityHandler> logger,
        CancellationToken cancellationToken)
    {
        if (!RouteIds.TryParse(eventId, out var id))
        {
            return ApiProblems.BadRequest("Path parameter 'eventId' must be a non-empty UUID.");
        }

        var result = await handler.HandleAsync(id, cancellationToken);
        if (result is null)
        {
            return ApiProblems.NotFound("Event not found.");
        }

        logger.LogInformation(
            "Air Quality response selected for event {EventId} with status {Status} and source {Source}.",
            id,
            result.Status,
            result.Source);
        return TypedResults.Ok(result.ToResponse());
    }
}
