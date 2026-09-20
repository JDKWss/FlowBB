using System.Net;
using System.Text.Json;
using FlowBB.Api.IntegrationTests.Endpoints;
using FluentAssertions;

namespace FlowBB.Api.IntegrationTests.Smoke;

[Collection(SmokeCollection.Name)]
public sealed class EventsSmokeTests : SmokeTestBase
{
    [SmokeFact]
    public async Task Health_ReportsHealthy()
    {
        var json = await Api.GetJsonAsync("/health");

        json.GetProperty("status").GetString().Should().Be("Healthy");
        json.TryGetProperty("timestamp", out _).Should().BeTrue();
    }

    [SmokeFact]
    public async Task Ready_ReportsHealthyWithARealNeo4j()
    {
        var json = await Api.GetJsonAsync("/health/ready");

        json.GetProperty("status").GetString().Should().Be("Healthy");
    }

    [SmokeFact]
    public async Task GetEvents_ListsTheSeededEvents()
    {
        var events = await Api.GetJsonAsync("/api/events");

        var concert = events.EnumerateArray().Should().Contain(item => item.GetProperty("id").GetGuid() == SmokeSeed.Concert)
            .Subject;
        concert.GetProperty("source").GetString().Should().Be("Demo");
        concert.GetProperty("participantsCount").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        concert.GetProperty("location").GetProperty("latitude").GetDouble().Should().BeInRange(-90, 90);
        events.GetArrayLength().Should().BeGreaterThanOrEqualTo(4);
    }

    [SmokeTheory]
    [InlineData("from=not-a-date")]
    [InlineData("from=2030-01-01T00:00:00Z&to=2020-01-01T00:00:00Z")]
    public async Task GetEvents_WithInvalidRange_Returns400(string query)
    {
        using var response = await Api.GetAsync($"/api/events?{query}");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }

    [SmokeFact]
    public async Task GetEventById_ReturnsDetails()
    {
        var details = await Api.GetJsonAsync($"/api/events/{SmokeSeed.Concert}");

        details.GetProperty("id").GetGuid().Should().Be(SmokeSeed.Concert);
        details.GetProperty("crewAvailable").ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);
        details.GetProperty("availableTransportModes").GetArrayLength().Should().BeGreaterThan(0);
    }

    [SmokeFact]
    public async Task GetEventById_WithUnknownEvent_Returns404()
    {
        using var response = await Api.GetAsync($"/api/events/{SmokeSeed.Unknown}");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.NotFound);
    }

    [SmokeTheory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task GetEventById_WithInvalidId_Returns400(string eventId)
    {
        using var response = await Api.GetAsync($"/api/events/{eventId}");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }
}
