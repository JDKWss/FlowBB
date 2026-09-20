using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FlowBB.Api.ExceptionHandling;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace FlowBB.Api.IntegrationTests.Startup;

/// <summary>Sprawdza zlozenie pipeline'u w prawdziwym Program.cs: wyjatek z glebi aplikacji daje bezpieczny ProblemDetails.</summary>
public sealed class ExceptionHandlingTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ThrowPath = "/__test/throw";
    private const string Secret = "Neo.ClientError.Security.Unauthorized neo4j://user:hunter2@db.example.io";
    private const string AllowedOrigin = "http://localhost:5173";

    private sealed class TraceCapture
    {
        public string? TraceId { get; set; }
    }

    // Program.cs czyta ILogEventSink z DI (ReadFrom.Services), wiec test przechwytuje prawdziwe zdarzenia logowania.
    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    // Dodaje na koncu pipeline'u (wewnatrz UseExceptionHandler) middleware, ktory rzuca wyjatek dla nieobsluzonej sciezki.
    private sealed class ThrowingStartupFilter(TraceCapture capture) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Use((HttpContext context, RequestDelegate _) =>
            {
                capture.TraceId = Activity.Current?.Id ?? context.TraceIdentifier;
                throw new InvalidOperationException(Secret);
            });
        };
    }

    private (HttpClient Client, TraceCapture Capture) CreateClient(string environment, CapturingSink? sink = null)
    {
        var capture = new TraceCapture();
        var client = factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IStartupFilter>(new ThrowingStartupFilter(capture));
                    if (sink is not null)
                    {
                        services.AddSingleton<ILogEventSink>(sink);
                    }
                });
            })
            .CreateClient();
        return (client, capture);
    }

    [Fact]
    public void Program_RegistersGlobalExceptionHandler()
    {
        var handlers = factory.Services.GetServices<IExceptionHandler>();

        handlers.Should().ContainSingle(handler => handler is GlobalExceptionHandler);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task UnhandledException_Returns500ProblemDetailsWithMatchingTraceId(string environment)
    {
        var (client, capture) = CreateClient(environment);

        using var response = await client.GetAsync(ThrowPath);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body).RootElement;

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        json.GetProperty("status").GetInt32().Should().Be(500);
        json.GetProperty("traceId").GetString().Should().Be(capture.TraceId).And.NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task UnhandledException_DoesNotExposeExceptionDetails(string environment)
    {
        var (client, _) = CreateClient(environment);

        using var response = await client.GetAsync(ThrowPath);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("hunter2").And.NotContain("neo4j://").And.NotContain("Neo.ClientError")
            .And.NotContain(nameof(InvalidOperationException)).And.NotContainEquivalentOf("stackTrace")
            .And.NotContain("   at ").And.NotContainEquivalentOf("<html");
    }

    [Fact]
    public async Task UnhandledException_IsLoggedOnceWithTheExceptionAndTheSameTraceIdAsTheResponse()
    {
        var sink = new CapturingSink();
        var (client, capture) = CreateClient("Production", sink);

        using var response = await client.GetAsync(ThrowPath);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        var withException = sink.Events.Where(logEvent => logEvent.Exception is not null).ToList();
        withException.Should().ContainSingle("wyjatek ma byc zalogowany dokladnie raz (bez duplikatu z middleware)");
        withException[0].Level.Should().Be(LogEventLevel.Error);
        withException[0].RenderMessage().Should().Contain(capture.TraceId!);
        json.GetProperty("traceId").GetString().Should().Be(capture.TraceId);
    }

    [Fact]
    public async Task ErrorResponse_KeepsCorsHeadersSoTheBrowserCanReadIt()
    {
        var (client, _) = CreateClient("Production");
        using var request = new HttpRequestMessage(HttpMethod.Get, ThrowPath);
        request.Headers.Add("Origin", AllowedOrigin);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle(AllowedOrigin);
    }

    [Fact]
    public async Task ExistingResponses_AreNotAffected()
    {
        using var client = factory.CreateClient();

        using var health = await client.GetAsync("/health");
        using var missing = await client.GetAsync("/does-not-exist");

        health.StatusCode.Should().Be(HttpStatusCode.OK);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
