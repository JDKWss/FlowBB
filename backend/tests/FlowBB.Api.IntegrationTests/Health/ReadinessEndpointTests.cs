using System.Net;
using System.Text.Json;
using FlowBB.Api.Extensions;
using FlowBB.Api.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Neo4j.Driver;

namespace FlowBB.Api.IntegrationTests.Health;

public sealed class ReadinessEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Secret = "Cannot connect to neo4j+s://user:hunter2@secret-host.example:7687";

    private HttpClient CreateClient(Func<Task> verifyConnectivity) => factory
        .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDriver>();
            services.AddSingleton(FakeDriverProxy.Create(verifyConnectivity));
        }))
        .CreateClient();

    [Fact]
    public async Task Ready_ReturnsHealthyWith200WhenNeo4jIsReachable()
    {
        using var client = CreateClient(() => Task.CompletedTask);

        using var response = await client.GetAsync(ReadinessExtensions.ReadinessPath);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        json.GetProperty("status").GetString().Should().Be("Healthy");
        json.TryGetProperty("timestamp", out _).Should().BeTrue();
        json.EnumerateObject().Should().HaveCount(2, "the response has the shape of HealthResponse and nothing more");
    }

    [Fact]
    public async Task Ready_Returns503WhenNeo4jIsNotReachable()
    {
        using var client = CreateClient(() => Task.FromException(new InvalidOperationException(Secret)));

        using var response = await client.GetAsync(ReadinessExtensions.ReadinessPath);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        JsonDocument.Parse(body).RootElement.GetProperty("status").GetString().Should().Be("Unhealthy");
    }

    [Fact]
    public async Task Ready_DoesNotLeakConnectionDetails()
    {
        using var client = CreateClient(() => Task.FromException(new InvalidOperationException(Secret)));

        using var response = await client.GetAsync(ReadinessExtensions.ReadinessPath);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("hunter2").And.NotContain("secret-host").And.NotContain("neo4j+s")
            .And.NotContain(nameof(InvalidOperationException)).And.NotContainEquivalentOf("exception");
    }

    [Fact]
    public async Task Liveness_StaysHealthyEvenWhenNeo4jIsDown()
    {
        using var client = CreateClient(() => Task.FromException(new InvalidOperationException(Secret)));

        using var live = await client.GetAsync("/health");
        using var ready = await client.GetAsync(ReadinessExtensions.ReadinessPath);

        live.StatusCode.Should().Be(HttpStatusCode.OK);
        ready.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Ready_IsNotCached()
    {
        using var client = CreateClient(() => Task.CompletedTask);

        using var response = await client.GetAsync(ReadinessExtensions.ReadinessPath);

        response.Headers.CacheControl?.NoStore.Should().BeTrue();
    }
}
