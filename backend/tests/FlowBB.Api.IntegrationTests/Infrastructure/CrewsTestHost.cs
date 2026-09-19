using FlowBB.Api.Endpoints.Crews;
using FlowBB.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowBB.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Minimalny host z modulem Crew i fake'ami portow. Program.cs jest wlasnoscia integracji startowej,
/// wiec testy endpointow Crew nie zaleza od jego zawartosci.
/// </summary>
public sealed class CrewsTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    public static readonly DateTimeOffset FixedNow = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    public HttpClient Client { get; }

    public FakeCrewRepository Crews { get; }

    private CrewsTestHost(WebApplication app, FakeCrewRepository crews)
    {
        _app = app;
        Crews = crews;
        Client = app.GetTestClient();
    }

    public static async Task<CrewsTestHost> StartAsync()
    {
        var crews = new FakeCrewRepository();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ICrewRepository>(crews);
        builder.Services.AddSingleton<IEventLookup>(
            new FakeEventLookup(
                FakeCrewRepository.EventId, FakeCrewRepository.OtherEventId, FakeCrewRepository.EmptyEventId));
        builder.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedNow));
        builder.Services.AddCrewModule();

        var app = builder.Build();
        app.MapCrewEndpoints();
        await app.StartAsync();
        return new CrewsTestHost(app, crews);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
