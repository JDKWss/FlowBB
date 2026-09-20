using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlowBB.Api.IntegrationTests.Hubs;

/// <summary>
/// Klient SignalR w przegladarce (dashboard) wysyla negocjacje z poswiadczeniami i wlasnymi naglowkami, wiec hub musi
/// przechodzic ta sama polityke CORS co REST. Origin dashboardu pochodzi z appsettings (Cors:AllowedOrigins).
/// </summary>
public sealed class PulseHubCorsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string NegotiateUrl = "/hubs/pulse/negotiate?negotiateVersion=1";
    private const string DashboardOrigin = "http://localhost:5173";

    [Fact]
    public async Task Preflight_FromTheDashboardOrigin_AllowsTheSignalRHeaders()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, NegotiateUrl);
        request.Headers.Add("Origin", DashboardOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "x-requested-with,x-signalr-user-agent");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be(DashboardOrigin);
        response.Headers.GetValues("Access-Control-Allow-Credentials").Should().ContainSingle().Which.Should().Be("true");
    }

    [Fact]
    public async Task Negotiate_FromTheDashboardOrigin_ReturnsAConnectionAndAllowsCredentials()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, NegotiateUrl);
        request.Headers.Add("Origin", DashboardOrigin);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be(DashboardOrigin);
        response.Headers.GetValues("Access-Control-Allow-Credentials").Should().ContainSingle().Which.Should().Be("true");
        (await response.Content.ReadAsStringAsync()).Should().Contain("connectionId");
    }

    [Fact]
    public async Task Negotiate_FromAnotherOrigin_GetsNoCorsHeaders()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, NegotiateUrl);
        request.Headers.Add("Origin", "http://evil.example");

        using var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }
}
