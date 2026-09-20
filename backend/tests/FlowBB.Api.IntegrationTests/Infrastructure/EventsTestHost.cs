using FlowBB.Api.Endpoints.Events;
using FlowBB.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowBB.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Minimalny host z modulem Events i fake'iem portu. Program.cs jest wlasnoscia integracji startowej,
/// wiec testy endpointow Events nie zaleza od jego zawartosci.
/// </summary>
public sealed class EventsTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    public HttpClient Client { get; }

    private EventsTestHost(WebApplication app)
    {
        _app = app;
        Client = app.GetTestClient();
    }

    public static async Task<EventsTestHost> StartAsync(IEventRepository repository)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddProblemDetails();
        builder.Services.AddEventsModule();
        builder.Services.AddSingleton(repository);

        var app = builder.Build();
        app.MapEventsEndpoints();
        await app.StartAsync();
        return new EventsTestHost(app);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }
}
