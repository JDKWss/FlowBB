using FlowBB.Api.Endpoints.Pulse;
using FlowBB.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowBB.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Minimalny host z modulem PULSE i fake'iem portu. Program.cs jest wlasnoscia integracji startowej,
/// wiec testy endpointow PULSE nie zaleza od jego zawartosci.
/// </summary>
public sealed class PulseTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    public HttpClient Client { get; }

    private PulseTestHost(WebApplication app)
    {
        _app = app;
        Client = app.GetTestClient();
    }

    public static async Task<PulseTestHost> StartAsync(IPulseDataReader reader)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddProblemDetails();
        builder.Services.AddPulseModule();
        builder.Services.AddSingleton(reader);

        var app = builder.Build();
        app.MapPulseEndpoints();
        await app.StartAsync();
        return new PulseTestHost(app);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }
}
