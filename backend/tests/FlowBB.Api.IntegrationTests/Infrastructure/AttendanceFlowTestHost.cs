using FlowBB.Api.Endpoints.Events;
using FlowBB.Api.Hubs;
using FlowBB.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowBB.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Host laczacy prawdziwe moduly Attendance i SignalR (bez Program.cs i bez bazy): endpointy Attendance
/// oraz hub /hubs/pulse z prawdziwym SignalRPulseNotifier. Persystencje zastepuje fake w pamieci.
/// </summary>
public sealed class AttendanceFlowTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    public HttpClient Client { get; }

    private AttendanceFlowTestHost(WebApplication app)
    {
        _app = app;
        Client = app.GetTestClient();
    }

    public static async Task<AttendanceFlowTestHost> StartAsync(
        IAttendanceRepository repository,
        IPulseDataReader? pulseReader = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddAttendanceModule();
        builder.Services.AddPulseHub();
        builder.Services.AddSingleton(repository);
        builder.Services.AddSingleton(pulseReader ?? new FakePulseDataReader());

        var app = builder.Build();
        app.MapAttendanceEndpoints();
        app.MapPulseHub();
        await app.StartAsync();
        return new AttendanceFlowTestHost(app);
    }

    public HubConnection CreateConnection()
    {
        var server = _app.GetTestServer();
        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "/hubs/pulse"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }
}
