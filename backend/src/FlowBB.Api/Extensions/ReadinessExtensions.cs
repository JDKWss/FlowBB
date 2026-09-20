using FlowBB.Api.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FlowBB.Api.Extensions;

public static class ReadinessExtensions
{
    public const string ReadinessPath = "/health/ready";

    private const string ReadyTag = "ready";

    public static IServiceCollection AddNeo4jReadiness(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHealthChecks().AddCheck<Neo4jReadinessCheck>("neo4j", tags: [ReadyTag]);
        return services;
    }

    /// <summary>
    /// <c>GET /health/ready</c>: 200 gdy Neo4j jest osiagalny, 503 gdy nie. <c>/health</c> pozostaje testem zywotnosci
    /// (200 bez zaleznosci od bazy). Odpowiedz ma ksztalt <c>HealthResponse</c> z OpenAPI i nie zawiera szczegolow bledu.
    /// </summary>
    public static IEndpointRouteBuilder MapReadinessEndpoint(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthChecks(ReadinessPath, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResponseWriter = WriteAsync
        }).WithName("getReadiness");
        return app;
    }

    private static Task WriteAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new HealthResponse(report.Status.ToString(), DateTimeOffset.UtcNow));
}
