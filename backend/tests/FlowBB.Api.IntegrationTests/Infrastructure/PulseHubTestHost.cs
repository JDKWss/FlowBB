using FlowBB.Api.Hubs;
using FlowBB.Application.Abstractions.Realtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowBB.Api.IntegrationTests.Infrastructure;

public sealed class PulseHubTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    public IPulseNotifier Notifier => _app.Services.GetRequiredService<IPulseNotifier>();

    private PulseHubTestHost(WebApplication app)
    {
        _app = app;
    }

    public static async Task<PulseHubTestHost> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddPulseHub();

        var app = builder.Build();
        app.MapPulseHub();
        await app.StartAsync();
        return new PulseHubTestHost(app);
    }

    public HubConnection CreateConnection()
    {
        var server = _app.GetTestServer();
        var hubUrl = new Uri(server.BaseAddress, "/hubs/pulse");

        return new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
