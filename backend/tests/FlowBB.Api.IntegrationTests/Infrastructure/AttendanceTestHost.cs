using FlowBB.Api.Endpoints.Events;
using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Abstractions.Realtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowBB.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Minimalny host z modulem Attendance, fake'iem repozytorium i notifierem zapisujacym komunikaty.
/// Program.cs jest wlasnoscia integracji startowej, wiec testy nie zaleza od jego zawartosci.
/// </summary>
public sealed class AttendanceTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    public HttpClient Client { get; }

    private AttendanceTestHost(WebApplication app)
    {
        _app = app;
        Client = app.GetTestClient();
    }

    public static async Task<AttendanceTestHost> StartAsync(IAttendanceRepository repository, IPulseNotifier notifier)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddProblemDetails();
        builder.Services.AddAttendanceModule();
        builder.Services.AddSingleton(repository);
        builder.Services.AddSingleton(notifier);

        var app = builder.Build();
        app.MapAttendanceEndpoints();
        await app.StartAsync();
        return new AttendanceTestHost(app);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }
}
