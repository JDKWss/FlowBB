namespace FlowBB.Infrastructure.Routing;

public sealed record RoutingServiceOptions(
    Uri ServiceUrl,
    TimeSpan Timeout,
    bool DemoFallbackEnabled);
