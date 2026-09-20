using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlowBB.Api.IntegrationTests;

public class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_HasTheShapeOfHealthResponse()
    {
        using var response = await factory.CreateClient().GetAsync("/health");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        json.GetProperty("status").GetString().Should().Be("Healthy");
        json.GetProperty("timestamp").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        json.EnumerateObject().Should().HaveCount(2, "the response has the shape of HealthResponse and nothing more");
    }
}
