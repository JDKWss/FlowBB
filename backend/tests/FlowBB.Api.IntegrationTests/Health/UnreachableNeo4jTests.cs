using System.Diagnostics;
using System.Net;
using FlowBB.Api.Extensions;
using FlowBB.Api.IntegrationTests.Startup;
using FlowBB.Infrastructure.Neo4j;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlowBB.Api.IntegrationTests.Health;

/// <summary>
/// Prawdziwy sterownik Neo4j skierowany na adres, pod ktorym nic nie nasluchuje. Sprawdza limity czasu: przy domyslnych
/// ustawieniach sterownika zadanie z dostepem do bazy wisialo ok. 37 s. Zmienne srodowiskowe sa procesowe, wiec klasa
/// dziala w kolekcji bez rownoleglosci.
/// </summary>
[Collection(CompositionTestCollection.Name)]
public sealed class UnreachableNeo4jTests
{
    // Z marginesem na wolniejsze maszyny; wartosc domyslna sterownika dawala ok. 37 s.
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(20);

    private static readonly Dictionary<string, string> UnreachableNeo4j = new()
    {
        [Neo4jOptions.UriVariable] = "neo4j://localhost:1",
        [Neo4jOptions.DatabaseVariable] = "flowbb-test",
        [Neo4jOptions.UsernameVariable] = "test-user",
        [Neo4jOptions.PasswordVariable] = "test-password"
    };

    private sealed class DevelopmentFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");
    }

    private static async Task<T> WithUnreachableNeo4jAsync<T>(Func<HttpClient, Task<T>> action)
    {
        var originals = UnreachableNeo4j.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var (key, value) in UnreachableNeo4j)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            using var factory = new DevelopmentFactory();
            using var client = factory.CreateClient();
            return await action(client);
        }
        finally
        {
            foreach (var (key, value) in originals)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    [Fact]
    public async Task Ready_Returns503WithinTheBudgetAndLivenessIsStillOk()
    {
        var (ready, elapsed, live) = await WithUnreachableNeo4jAsync(async client =>
        {
            var stopwatch = Stopwatch.StartNew();
            using var readyResponse = await client.GetAsync(ReadinessExtensions.ReadinessPath);
            var readyElapsed = stopwatch.Elapsed;
            using var liveResponse = await client.GetAsync("/health");
            return (readyResponse.StatusCode, readyElapsed, liveResponse.StatusCode);
        });

        ready.Should().Be(HttpStatusCode.ServiceUnavailable);
        elapsed.Should().BeLessThan(Budget);
        live.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RequestNeedingTheDatabase_FailsWithProblemDetailsWithinTheBudget()
    {
        var (status, contentType, elapsed) = await WithUnreachableNeo4jAsync(async client =>
        {
            var stopwatch = Stopwatch.StartNew();
            using var response = await client.GetAsync("/api/events");
            return (response.StatusCode, response.Content.Headers.ContentType?.MediaType, stopwatch.Elapsed);
        });

        status.Should().Be(HttpStatusCode.InternalServerError);
        contentType.Should().Be("application/problem+json");
        elapsed.Should().BeLessThan(Budget);
    }
}
