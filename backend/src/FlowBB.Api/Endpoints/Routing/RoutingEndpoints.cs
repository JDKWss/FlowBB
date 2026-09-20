using FlowBB.Api.Endpoints;
using FlowBB.Application.Abstractions.Routing;
using FlowBB.Application.Routing.GetEventRoute;
using FlowBB.Infrastructure.Routing;
using FlowBB.Infrastructure.Routing.Mzk;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlowBB.Api.Endpoints.Routing;

public static class RoutingEndpoints
{
    /// <summary>
    /// Rejestruje modul trasy dla wybranego trybu. W trybie <see cref="RoutingMode.Demo"/> <c>IRoutePlanner</c> to
    /// <c>TimetableFallbackRoutePlanner</c>: komunikacja miejska z rozkladu MZK, a pozostale tryby i przypadki, ktorych
    /// rozklad nie umie zaplanowac, ida do <c>DemoRoutePlanner</c> (bez wywolan uslugi drogowej). W
    /// <see cref="RoutingMode.RoadRouting"/> planer zlozony z fallbackiem.
    /// </summary>
    public static IServiceCollection AddRoutingModule(this IServiceCollection services, RoutingMode mode)
    {
        if (mode == RoutingMode.Demo)
        {
            // Musi byc przed AddRoutingModule(): TryAdd zachowuje pierwsza rejestracje IRoutePlanner. Domyslny tryb Demo nie
            // uzywa CompositeRoutePlanner, wiec bez tego planer z rozkladu byl by martwym kodem.
            services.AddTimetableRouting();
            services.TryAddTransient<IRoutePlanner>(provider => provider.GetRequiredService<TimetableFallbackRoutePlanner>());
        }

        return services.AddRoutingModule();
    }

    public static IServiceCollection AddRoutingModule(this IServiceCollection services)
    {
        services.AddTimetableRouting();
        services.TryAddTransient<RoutingServiceRoutePlanner>();
        services.TryAddTransient<CompositeRoutePlanner>();
        services.TryAddTransient<IRoutePlanner>(provider => provider.GetRequiredService<CompositeRoutePlanner>());
        services.AddScoped<GetEventRouteHandler>();
        return services;
    }

    private static void AddTimetableRouting(this IServiceCollection services)
    {
        services.TryAddSingleton<DemoRoutePlanner>();
        services.TryAddSingleton<MzkTimetableProvider>();
        services.TryAddSingleton<MzkTimetableRoutePlanner>();
        services.TryAddSingleton<TimetableFallbackRoutePlanner>();
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
