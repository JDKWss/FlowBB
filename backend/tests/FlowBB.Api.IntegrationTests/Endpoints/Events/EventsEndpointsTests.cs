using System.Net;
using System.Text.Json;
using FlowBB.Api.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace FlowBB.Api.IntegrationTests.Endpoints.Events;

public class EventsEndpointsTests
{
    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static void AssertProblem(HttpResponseMessage response, HttpStatusCode expected)
    {
        response.StatusCode.Should().Be(expected);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task GetEvents_WithNoEvents_ReturnsEmptyArray()
    {
        await using var host = await EventsTestHost.StartAsync(new FakeEventRepository());

        using var response = await host.Client.GetAsync("/api/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = await ReadJsonAsync(response);
        document.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        document.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetEvents_ReturnsEventsSortedByStartWithParticipantCounts()
    {
        await using var host = await EventsTestHost.StartAsync(FakeEventRepository.WithDemoEvents());

        using var response = await host.Client.GetAsync("/api/events");

        using var document = await ReadJsonAsync(response);
        var items = document.RootElement.EnumerateArray().ToArray();
        items.Select(item => item.GetProperty("name").GetString())
            .Should().Equal("Koncert na Rynku", "Wieczor z Planszowkami");
        items.Select(item => item.GetProperty("participantsCount").GetInt32()).Should().Equal(82, 0);
    }

    [Fact]
    public async Task GetEvents_ReturnsEnumsAsNamesAndTimesInWarsawZone()
    {
        await using var host = await EventsTestHost.StartAsync(FakeEventRepository.WithDemoEvents());

        using var response = await host.Client.GetAsync("/api/events");

        using var document = await ReadJsonAsync(response);
        var concert = document.RootElement[0];
        concert.GetProperty("category").GetString().Should().Be("Culture");
        concert.GetProperty("source").GetString().Should().Be("Demo");
        concert.GetProperty("startAt").GetString().Should().Be("2026-09-25T19:00:00+02:00");
        concert.GetProperty("endAt").GetString().Should().Be("2026-09-25T21:30:00+02:00");
        concert.GetProperty("location").GetProperty("latitude").GetDouble().Should().Be(49.82245);
    }

    [Fact]
    public async Task GetEvents_ForEventWithoutEndAt_ReturnsNullEndAt()
    {
        await using var host = await EventsTestHost.StartAsync(FakeEventRepository.WithDemoEvents());

        using var response = await host.Client.GetAsync("/api/events");

        using var document = await ReadJsonAsync(response);
        document.RootElement[1].GetProperty("endAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetEvents_ForwardsRangeToRepositoryAndFilters()
    {
        var repository = FakeEventRepository.WithDemoEvents();
        await using var host = await EventsTestHost.StartAsync(repository);

        using var response = await host.Client.GetAsync(
            "/api/events?from=2026-09-30T00:00:00%2B02:00&to=2026-10-05T00:00:00%2B02:00");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        repository.LastFrom.Should().Be(new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.FromHours(2)));
        repository.LastTo.Should().Be(new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.FromHours(2)));
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetArrayLength().Should().Be(1);
    }

    [Theory]
    [InlineData("/api/events?from=not-a-date")]
    [InlineData("/api/events?to=2026-13-45")]
    [InlineData("/api/events?from=2026-10-05T00:00:00Z&to=2026-09-30T00:00:00Z")]
    public async Task GetEvents_WithInvalidRange_ReturnsBadRequestProblem(string url)
    {
        await using var host = await EventsTestHost.StartAsync(FakeEventRepository.WithDemoEvents());

        using var response = await host.Client.GetAsync(url);

        AssertProblem(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetEvent_ReturnsDetailsWithMvpDefaults()
    {
        await using var host = await EventsTestHost.StartAsync(FakeEventRepository.WithDemoEvents());

        using var response = await host.Client.GetAsync($"/api/events/{FakeEventRepository.ConcertId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;
        root.GetProperty("id").GetGuid().Should().Be(FakeEventRepository.ConcertId);
        root.GetProperty("participantsCount").GetInt32().Should().Be(82);
        root.GetProperty("crewAvailable").GetBoolean().Should().BeFalse();
        root.GetProperty("availableTransportModes").EnumerateArray().Select(mode => mode.GetString())
            .Should().Equal("Walking", "PublicTransport", "Bike", "Car");
    }

    [Fact]
    public async Task GetEvent_WithUnknownId_ReturnsNotFoundProblem()
    {
        await using var host = await EventsTestHost.StartAsync(FakeEventRepository.WithDemoEvents());

        using var response = await host.Client.GetAsync("/api/events/99999999-9999-9999-9999-999999999999");

        AssertProblem(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task GetEvent_WithInvalidId_ReturnsBadRequestProblem(string eventId)
    {
        await using var host = await EventsTestHost.StartAsync(FakeEventRepository.WithDemoEvents());

        using var response = await host.Client.GetAsync($"/api/events/{eventId}");

        AssertProblem(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Responses_DoNotExposeUserData()
    {
        await using var host = await EventsTestHost.StartAsync(FakeEventRepository.WithDemoEvents());

        var list = await host.Client.GetStringAsync("/api/events");
        var details = await host.Client.GetStringAsync($"/api/events/{FakeEventRepository.ConcertId}");

        (list + details).ToLowerInvariant().Should().NotContain("userid");
    }
}
