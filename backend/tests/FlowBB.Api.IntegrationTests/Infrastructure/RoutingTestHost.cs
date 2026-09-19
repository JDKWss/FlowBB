using FlowBB.Api.Endpoints.Routing;
using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Abstractions.Routing;
using FlowBB.Application.Routing;
using FlowBB.Domain.Common;
using FlowBB.Domain.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowBB.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Minimalny host z modulem Routing i fake'ami portow. Program.cs jest wlasnoscia integracji startowej,
/// wiec testy endpointu trasy nie zaleza od jego zawartosci. Domyslnie uzywany jest prawdziwy DemoRoutePlanner.
/// </summary>
public sealed class RoutingTestHost : IAsyncDisposable
{
    public static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid AttendingUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly WebApplication _app;

    public HttpClient Client { get; }

    private RoutingTestHost(WebApplication app)
    {
        _app = app;
        Client = app.GetTestClient();
    }

    public static Event CreateEvent(DateTimeOffset? endAt = null) => new(
        EventId,
        "Koncert na Rynku",
        "Wieczorny koncert.",
        new DateTimeOffset(2026, 9, 25, 17, 0, 0, TimeSpan.Zero),
        endAt ?? new DateTimeOffset(2026, 9, 25, 19, 30, 0, TimeSpan.Zero),
        "Rynek w Bielsku-Bialej",
        EventCategory.Culture,
        EventSource.Demo,
        new GeoPoint(49.82245, 19.04431));

    public static async Task<RoutingTestHost> StartAsync(
        Event? existingEvent,
        AttendanceOrigin? attendance,
        IRoutePlanner? planner = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IEventLookup>(new FakeEventLookup(existingEvent));
        builder.Services.AddSingleton<IAttendanceOriginLookup>(new FakeAttendanceOriginLookup(attendance));
        if (planner is not null)
        {
            builder.Services.AddSingleton(planner);
        }

        builder.Services.AddRoutingModule();

        var app = builder.Build();
        app.MapRoutingEndpoints();
        await app.StartAsync();
        return new RoutingTestHost(app);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }

    private sealed class FakeEventLookup(Event? existing) : IEventLookup
    {
        public Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(existing?.Id == eventId);

        public Task<Event?> FindByIdAsync(Guid eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(existing?.Id == eventId ? existing : null);
    }

    private sealed class FakeAttendanceOriginLookup(AttendanceOrigin? attendance) : IAttendanceOriginLookup
    {
        public Task<AttendanceOrigin?> FindAsync(
            Guid eventId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(userId == AttendingUserId ? attendance : null);
    }
}
