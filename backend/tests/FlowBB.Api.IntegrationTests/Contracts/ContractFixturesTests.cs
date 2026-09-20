using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowBB.Api.IntegrationTests.Infrastructure;
using FlowBB.Application.Pulse;
using FlowBB.Application.Routing;
using FlowBB.Domain.Common;
using FluentAssertions;

namespace FlowBB.Api.IntegrationTests.Contracts;

public sealed class ContractFixturesTests
{
    private const double PulseOriginLatitude = 49.8225;
    private const double PulseOriginLongitude = 19.0444;

    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherEventId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task EventFixtures_MatchRuntimeShapes()
    {
        await using var host = await EventsTestHost.StartAsync(FakeEventRepository.WithDemoEvents());

        using var list = await host.Client.GetAsync("/api/events");
        using var details = await host.Client.GetAsync($"/api/events/{FakeEventRepository.ConcertId}");

        await AssertSuccessFixtureAsync("events-list.json", list);
        await AssertSuccessFixtureAsync("event-details.json", details);
    }

    [Fact]
    public async Task AttendanceFixtures_MatchNewAndRepeatedRuntimeShapes()
    {
        var repository = new FakeAttendanceRepository().AddEvent(EventId).AddUser(UserId);
        await using var host = await AttendanceTestHost.StartAsync(repository, new RecordingPulseNotifier());
        var request = new { userId = UserId, transportMode = "PublicTransport" };

        using var created = await host.Client.PostAsJsonAsync($"/api/events/{EventId}/attendance", request);
        using var repeated = await host.Client.PostAsJsonAsync($"/api/events/{EventId}/attendance", request);

        await AssertSuccessFixtureAsync("attendance-created.json", created);
        await AssertSuccessFixtureAsync("attendance-repeated.json", repeated);
    }

    [Fact]
    public async Task PulseFixtures_MatchRuntimeShapes()
    {
        var reader = new FakePulseDataReader()
            .AddEvent(EventId, "DEMO DATA / SYMULACJA - Koncert", Points(6, TransportMode.PublicTransport)
                .Concat(Points(4, TransportMode.Bike)))
            .AddEvent(OtherEventId, "DEMO DATA / SYMULACJA - Mecz", Points(2, TransportMode.Car));
        await using var host = await PulseTestHost.StartAsync(reader);

        using var summary = await host.Client.GetAsync("/api/pulse/summary");
        using var eventPulse = await host.Client.GetAsync($"/api/pulse/events/{EventId}");
        using var populated = await host.Client.GetAsync($"/api/pulse/hexagons?eventId={EventId}");
        using var empty = await host.Client.GetAsync($"/api/pulse/hexagons?eventId={OtherEventId}");

        await AssertSuccessFixtureAsync("pulse-summary.json", summary);
        await AssertSuccessFixtureAsync("pulse-event.json", eventPulse);
        await AssertSuccessFixtureAsync("pulse-hexagons.geojson", populated);
        await AssertSuccessFixtureAsync("pulse-hexagons-empty.geojson", empty);
    }

    [Fact]
    public async Task GroupsFixture_MatchesRuntimeShape()
    {
        await using var host = await CrewsTestHost.StartAsync();

        using var response = await host.Client.GetAsync($"/api/events/{FakeCrewRepository.EventId}/groups");

        await AssertSuccessFixtureAsync("groups.json", response);
    }

    [Fact]
    public async Task RouteFixture_MatchesRuntimeShape()
    {
        var origin = new AttendanceOrigin(
            new GeoPoint(49.798, 19.08),
            TransportMode.PublicTransport);
        await using var host = await RoutingTestHost.StartAsync(RoutingTestHost.CreateEvent(), origin);

        using var response = await host.Client.GetAsync(
            $"/api/events/{RoutingTestHost.EventId}/route?userId={RoutingTestHost.AttendingUserId}");

        await AssertSuccessFixtureAsync("route.json", response);
    }

    [Fact]
    public async Task ProblemFixtures_MatchRuntimeShapes()
    {
        await using var eventsHost = await EventsTestHost.StartAsync(FakeEventRepository.WithDemoEvents());
        using var badRequest = await eventsHost.Client.GetAsync("/api/events/not-a-guid");
        using var notFound = await eventsHost.Client.GetAsync("/api/events/99999999-9999-9999-9999-999999999999");

        await using var crewsHost = await CrewsTestHost.StartAsync();
        crewsHost.Crews.Seed(FakeCrewRepository.TinyCrewId, FakeCrewRepository.UserA);
        crewsHost.Crews.Seed(FakeCrewRepository.TinyCrewId, FakeCrewRepository.UserB);
        using var conflict = await crewsHost.Client.PostAsJsonAsync(
            $"/api/groups/{FakeCrewRepository.TinyCrewId}/members",
            new { userId = FakeCrewRepository.UserC });

        await AssertProblemFixtureAsync("problem-400.json", badRequest, HttpStatusCode.BadRequest);
        await AssertProblemFixtureAsync("problem-404.json", notFound, HttpStatusCode.NotFound);
        await AssertProblemFixtureAsync("problem-409.json", conflict, HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("pulse-summary.json")]
    [InlineData("pulse-event.json")]
    [InlineData("pulse-hexagons.geojson")]
    [InlineData("pulse-hexagons-empty.geojson")]
    public async Task PulseFixtures_DoNotExposeUserData(string fixtureName)
    {
        var json = await File.ReadAllTextAsync(FixturePath(fixtureName));

        json.Should().NotContainEquivalentOf("userId");
        json.Should().NotContain(PulseOriginLatitude.ToString(System.Globalization.CultureInfo.InvariantCulture));
        json.Should().NotContain(PulseOriginLongitude.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task PopulatedHexagonFixture_ContainsOnlyPublishableCells()
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(FixturePath("pulse-hexagons.geojson")));
        var features = fixture.RootElement.GetProperty("features").EnumerateArray();

        features.Should().NotBeEmpty().And.OnlyContain(feature =>
            feature.GetProperty("properties").GetProperty("participants").GetInt32() >= 10);
    }

    private static IEnumerable<PulsePoint> Points(int count, TransportMode mode) =>
        Enumerable.Range(0, count).Select(_ => new PulsePoint(PulseOriginLatitude, PulseOriginLongitude, mode));

    private static async Task AssertSuccessFixtureAsync(string fixtureName, HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        await AssertFixtureShapeAsync(fixtureName, response);
    }

    private static async Task AssertProblemFixtureAsync(
        string fixtureName,
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        response.StatusCode.Should().Be(expectedStatus);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        await AssertFixtureShapeAsync(fixtureName, response);
    }

    private static async Task AssertFixtureShapeAsync(string fixtureName, HttpResponseMessage response)
    {
        using var expected = JsonDocument.Parse(await File.ReadAllTextAsync(FixturePath(fixtureName)));
        await using var actualStream = await response.Content.ReadAsStreamAsync();
        using var actual = await JsonDocument.ParseAsync(actualStream);

        GetShape(actual.RootElement).Should().Be(
            GetShape(expected.RootElement),
            $"fixture '{fixtureName}' must expose the same JSON fields and types as the API response");
    }

    private static string GetShape(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => GetObjectShape(element),
        JsonValueKind.Array => GetArrayShape(element),
        _ => element.ValueKind.ToString()
    };

    private static string GetObjectShape(JsonElement element)
    {
        var properties = element.EnumerateObject()
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => $"{property.Name}:{GetShape(property.Value)}");
        return $"object{{{string.Join(",", properties)}}}";
    }

    private static string GetArrayShape(JsonElement element)
    {
        var itemShapes = element.EnumerateArray()
            .Select(GetShape)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        return $"array[{string.Join("|", itemShapes)}]";
    }

    private static string FixturePath(string fixtureName) =>
        Path.Combine(AppContext.BaseDirectory, "Contracts", "Fixtures", fixtureName);
}
