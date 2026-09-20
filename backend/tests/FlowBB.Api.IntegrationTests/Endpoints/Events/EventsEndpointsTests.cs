using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowBB.Api.IntegrationTests.Endpoints;
using FlowBB.Api.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace FlowBB.Api.IntegrationTests.Endpoints.Events;

public class EventsEndpointsTests
{
    private static readonly object ValidCreateRequest = new
    {
        name = "FlowBB Demo Event",
        description = "Event added live from the organizer dashboard.",
        startAt = "2026-09-20T19:00:00+02:00",
        endAt = "2026-09-20T22:00:00+02:00",
        venueName = "Plac Bolesława Chrobrego",
        category = "Community",
        location = new { latitude = 49.8215, longitude = 19.0455 }
    };

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
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
    public async Task CreateEvent_WithValidRequest_ReturnsCreatedExternalEventAndAddsItToList()
    {
        var repository = new FakeEventRepository();
        await using var host = await EventsTestHost.StartAsync(repository);

        using var response = await host.Client.PostAsJsonAsync("/api/events", ValidCreateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var document = await ReadJsonAsync(response);
        var created = document.RootElement;
        var eventId = created.GetProperty("id").GetGuid();
        eventId.Should().NotBe(Guid.Empty);
        created.GetProperty("source").GetString().Should().Be("External");
        created.GetProperty("participantsCount").GetInt32().Should().Be(0);
        created.GetProperty("location").GetProperty("latitude").GetDouble().Should().Be(49.8215);
        response.Headers.Location.Should().Be(new Uri($"/api/events/{eventId:D}", UriKind.Relative));
        repository.LastCreatedVenueId.Should().StartWith("external-venue-");

        using var listResponse = await host.Client.GetAsync("/api/events");
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        list.EnumerateArray().Should().Contain(item => item.GetProperty("id").GetGuid() == eventId);
    }

    [Theory]
    [InlineData(-90.1, 19.0455)]
    [InlineData(90.1, 19.0455)]
    [InlineData(49.8215, -180.1)]
    [InlineData(49.8215, 180.1)]
    public async Task CreateEvent_WithInvalidCoordinates_ReturnsBadRequest(double latitude, double longitude)
    {
        await using var host = await EventsTestHost.StartAsync(new FakeEventRepository());
        var request = new
        {
            name = "Invalid location",
            description = "",
            startAt = "2026-09-20T19:00:00+02:00",
            endAt = (string?)null,
            venueName = "Bielsko-Biała",
            category = "Community",
            location = new { latitude, longitude }
        };

        using var response = await host.Client.PostAsJsonAsync("/api/events", request);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEvent_WithEndBeforeStart_ReturnsBadRequest()
    {
        await using var host = await EventsTestHost.StartAsync(new FakeEventRepository());
        var request = new
        {
            name = "Invalid time",
            description = "",
            startAt = "2026-09-20T22:00:00+02:00",
            endAt = "2026-09-20T19:00:00+02:00",
            venueName = "Bielsko-Biała",
            category = "Community",
            location = new { latitude = 49.8215, longitude = 19.0455 }
        };

        using var response = await host.Client.PostAsJsonAsync("/api/events", request);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEvent_WithMalformedTimestamp_ReturnsBadRequest()
    {
        await using var host = await EventsTestHost.StartAsync(new FakeEventRepository());
        const string json = """
            {
              "name": "Invalid time",
              "description": "",
              "startAt": "not-an-instant",
              "venueName": "Bielsko-Biała",
              "category": "Community",
              "location": { "latitude": 49.8215, "longitude": 19.0455 }
            }
            """;

        using var response = await host.Client.PostAsync(
            "/api/events",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEvent_WithInvalidCategory_ReturnsBadRequest()
    {
        await using var host = await EventsTestHost.StartAsync(new FakeEventRepository());
        var request = new
        {
            name = "Invalid category",
            description = "",
            startAt = "2026-09-20T19:00:00+02:00",
            endAt = (string?)null,
            venueName = "Bielsko-Biała",
            category = "Concert",
            location = new { latitude = 49.8215, longitude = 19.0455 }
        };

        using var response = await host.Client.PostAsJsonAsync("/api/events", request);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
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

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
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

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task GetEvent_WithInvalidId_ReturnsBadRequestProblem(string eventId)
    {
        await using var host = await EventsTestHost.StartAsync(FakeEventRepository.WithDemoEvents());

        using var response = await host.Client.GetAsync($"/api/events/{eventId}");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
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
