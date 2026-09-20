using FlowBB.Api.IntegrationTests.Infrastructure;
using FlowBB.Api.Logging;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace FlowBB.Api.IntegrationTests.Startup;

/// <summary>Sprawdza logowanie w prawdziwym pipeline Program.cs: log startowy, korelacja zadania i brak danych wrazliwych.</summary>
public sealed class StructuredLoggingTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Password = "s3cretpass";
    private const string Username = "neo4juser";

    private static readonly Guid EventGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CrewGuid = Guid.Parse("55555555-5555-5555-5555-555555555555");

    // Mapuje trasy z parametrami, ktore w Program.cs sa jeszcze niepodpiete (moduly czekaja na adaptery), aby sprawdzic wzbogacenie z trasy.
    private sealed class ProbeRoutesStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/api/events/{eventId}/probe", () => Results.Ok());
                endpoints.MapGet("/api/groups/{groupId}/probe", () => Results.Ok());
            });
        };
    }

    private HttpClient CreateClient(CapturingSink sink, bool withSecrets = false) => factory
        .WithWebHostBuilder(builder =>
        {
            if (withSecrets)
            {
                builder.UseSetting("NEO4J_URI", $"neo4j+s://{Username}:{Password}@abc.databases.neo4j.io:7687");
                builder.UseSetting("NEO4J_DATABASE", "flowbb");
                builder.UseSetting("NEO4J_USERNAME", Username);
                builder.UseSetting("NEO4J_PASSWORD", Password);
            }

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<ILogEventSink>(sink);
                services.AddSingleton<IStartupFilter, ProbeRoutesStartupFilter>();
            });
        })
        .CreateClient();

    private static LogEvent RequestSummaryFor(CapturingSink sink, string path) => sink.Events.Single(logEvent =>
        logEvent.Properties.TryGetValue("RequestPath", out var value) && value.ToString().Contains(path));

    private static object? ScalarOf(LogEvent logEvent, string property) => ((ScalarValue)logEvent.Properties[property]).Value;

    [Fact]
    public void StartupLog_ContainsHostAndDatabaseButNoCredentials()
    {
        var sink = new CapturingSink();
        using var client = CreateClient(sink, withSecrets: true);

        var startup = sink.Events.Should().ContainSingle(logEvent => logEvent.RenderMessage().Contains("FlowBB API starting")).Subject;

        ScalarOf(startup, "Neo4jHost").Should().Be("abc.databases.neo4j.io");
        ScalarOf(startup, "Neo4jDatabase").Should().Be("flowbb");
        startup.Properties.Should().ContainKey("Environment").And.ContainKey("CorsOrigins");
        sink.Events.Select(CapturingSink.Flatten).Should().NotContain(text => text.Contains(Password) || text.Contains(Username));
    }

    [Fact]
    public void StartupLog_WithoutNeo4jSettings_SaysNotSetInsteadOfFailing()
    {
        var sink = new CapturingSink();
        using var client = CreateClient(sink);

        var startup = sink.Events.Should().ContainSingle(logEvent => logEvent.RenderMessage().Contains("FlowBB API starting")).Subject;

        startup.Properties.Should().ContainKey("Neo4jHost");
    }

    [Fact]
    public async Task RequestSummary_CarriesTheTraceId()
    {
        var sink = new CapturingSink();
        using var client = CreateClient(sink);

        using var response = await client.GetAsync("/health");

        var summary = RequestSummaryFor(sink, "/health");
        summary.Properties.Should().ContainKey(RequestLogContextMiddleware.TraceIdProperty);
        ScalarOf(summary, RequestLogContextMiddleware.TraceIdProperty)!.ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RequestSummary_CarriesTheEventIdFromTheRoute()
    {
        var sink = new CapturingSink();
        using var client = CreateClient(sink);

        using var response = await client.GetAsync($"/api/events/{EventGuid}/probe");

        response.EnsureSuccessStatusCode();
        var summary = RequestSummaryFor(sink, "/probe");
        ScalarOf(summary, RequestLogContextMiddleware.EventIdProperty).Should().Be(EventGuid);
        summary.Properties.Should().NotContainKey(RequestLogContextMiddleware.CrewIdProperty);
    }

    [Fact]
    public async Task RequestSummary_CarriesTheCrewIdFromTheRoute()
    {
        var sink = new CapturingSink();
        using var client = CreateClient(sink);

        using var response = await client.GetAsync($"/api/groups/{CrewGuid}/probe");

        response.EnsureSuccessStatusCode();
        ScalarOf(RequestSummaryFor(sink, "/probe"), RequestLogContextMiddleware.CrewIdProperty).Should().Be(CrewGuid);
    }
}
