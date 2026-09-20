using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlowBB.Api.IntegrationTests.Startup;

public sealed class StartupTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string AllowedOrigin = "http://localhost:5173";

    [Fact]
    public async Task CorsPreflight_FromAllowedOrigin_ReturnsCorsHeaders()
    {
        using var response = await SendPreflightAsync(AllowedOrigin);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle(AllowedOrigin);
        response.Headers.GetValues("Access-Control-Allow-Credentials").Should().ContainSingle("true");
        response.Headers.GetValues("Access-Control-Allow-Methods").Should().Contain("GET");
    }

    [Fact]
    public async Task CorsPreflight_FromDisallowedOrigin_DoesNotReturnCorsHeaders()
    {
        using var response = await SendPreflightAsync("https://not-allowed.example");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
        response.Headers.Contains("Access-Control-Allow-Credentials").Should().BeFalse();
    }

    [Fact]
    public async Task PulseHubNegotiate_ReturnsOk()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/hubs/pulse/negotiate?negotiateVersion=1", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpResponseMessage> SendPreflightAsync(string origin)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return await client.SendAsync(request);
    }
}
