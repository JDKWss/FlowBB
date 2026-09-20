using System.Collections.Concurrent;
using FlowBB.Api.Endpoints.AirQuality;
using FlowBB.Application.Abstractions.AirQuality;
using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.AirQuality;
using FlowBB.Application.AirQuality.GetEventAirQuality;
using FlowBB.Domain.Common;
using FlowBB.Domain.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowBB.Api.IntegrationTests.Infrastructure;

public sealed class AirQualityTestHost : IAsyncDisposable
{
    public static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly GeoPoint EventLocation = new(49.82245, 19.04431);

    private readonly WebApplication _app;

    public HttpClient Client { get; }

    public ConcurrentQueue<string> Logs { get; }

    private AirQualityTestHost(WebApplication app, ConcurrentQueue<string> logs)
    {
        _app = app;
        Client = app.GetTestClient();
        Logs = logs;
    }

    public static async Task<AirQualityTestHost> StartAsync(
        IAirQualityProvider provider,
        IAirQualityFallbackProvider? fallback = null,
        DateTimeOffset? now = null)
    {
        var logs = new ConcurrentQueue<string>();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new RecordingLoggerProvider(logs));
        builder.Services.AddProblemDetails();
        builder.Services.AddSingleton<IEventLookup>(new EventLookup());
        builder.Services.AddSingleton(provider);
        builder.Services.AddSingleton(fallback ?? new NeverUsedFallback());
        builder.Services.AddSingleton<IAirQualityCache>(new PassThroughCache());
        builder.Services.AddSingleton<TimeProvider>(
            new FixedTimeProvider(now ?? DateTimeOffset.Parse("2026-09-20T10:30:00+02:00")));
        builder.Services.AddSingleton(
            new AirQualityPolicyOptions(
                TimeSpan.FromMinutes(20),
                TimeSpan.FromSeconds(1),
                TimeSpan.FromMinutes(90)));
        builder.Services.AddScoped<GetEventAirQualityHandler>();

        var app = builder.Build();
        app.MapAirQualityEndpoints();
        await app.StartAsync();
        return new AirQualityTestHost(app, logs);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }

    private sealed class EventLookup : IEventLookup
    {
        public Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(eventId == EventId);

        public Task<Event?> FindByIdAsync(Guid eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(eventId == EventId ? DemoEvent() : null);
    }

    private sealed class NeverUsedFallback : IAirQualityFallbackProvider
    {
        public Task<EventAirQuality> GetAsync(
            Guid eventId,
            GeoPoint eventLocation,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Fallback was not expected in this endpoint test.");
    }

    private sealed class PassThroughCache : IAirQualityCache
    {
        public Task<T> GetOrCreateAsync<T>(
            Guid eventId,
            TimeSpan lifetime,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken cancellationToken = default)
            where T : class => factory(cancellationToken);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingLoggerProvider(ConcurrentQueue<string> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(logs);

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger(ConcurrentQueue<string> logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            logs.Enqueue(formatter(state, exception));
    }

    private static Event DemoEvent() => new(
        EventId,
        "Koncert na Rynku",
        "Demo",
        DateTimeOffset.Parse("2026-09-20T19:00:00+02:00"),
        DateTimeOffset.Parse("2026-09-20T21:30:00+02:00"),
        "Rynek",
        EventCategory.Culture,
        EventSource.Demo,
        EventLocation);
}
