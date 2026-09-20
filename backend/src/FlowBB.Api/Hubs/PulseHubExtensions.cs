using FlowBB.Application.Abstractions.Realtime;

namespace FlowBB.Api.Hubs;

public static class PulseHubExtensions
{
    public static IServiceCollection AddPulseHub(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSignalR();
        services.AddSingleton<IPulseNotifier, SignalRPulseNotifier>();
        return services;
    }

    public static IEndpointRouteBuilder MapPulseHub(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHub<PulseHub>("/hubs/pulse");
        return endpoints;
    }
}
