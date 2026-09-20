using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowBB.Api.IntegrationTests.Endpoints;
using FlowBB.Api.IntegrationTests.Infrastructure;
using FlowBB.Application.Pulse;
using FlowBB.Domain.Common;
using FluentAssertions;

namespace FlowBB.Api.IntegrationTests.Endpoints.Pulse;

public class PulseEndpointsTests
{
    private const double Latitude = 49.8225;
    private const double Longitude = 19.0444;

    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherEventId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static IEnumerable<PulsePoint> Points(int count, TransportMode mode, double latitude = Latitude) =>
        Enumerable.Range(0, count).Select(_ => new PulsePoint(latitude, Longitude, mode));

    private static async Task<PulseTestHost> HostAsync(Action<FakePulseDataReader>? seed = null)
    {
        var reader = new FakePulseDataReader();
        seed?.Invoke(reader);
        return await PulseTestHost.StartAsync(reader);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Summary_ReturnsContractShape()
    {
        await using var host = await HostAsync(r => r
            .AddEvent(EventId, "Koncert", Points(3, TransportMode.Walking))
            .AddEvent(OtherEventId, "Mecz", Points(2, TransportMode.Car)));

        var response = await host.Client.GetAsync("/api/pulse/summary");
        var json = await JsonAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.GetProperty("eventsCount").GetInt32().Should().Be(2);
        json.GetProperty("participantsCount").GetInt32().Should().Be(5);
        json.GetProperty("participantsWithoutReturn").GetInt32().Should().Be(0);
        json.GetProperty("modalSplit").GetProperty("walking").GetInt32().Should().Be(3);
        json.GetProperty("modalSplit").GetProperty("car").GetInt32().Should().Be(2);
        json.TryGetProperty("generatedAt", out _).Should().BeTrue();
    }

    [Fact]
    public async Task EventPulse_ReturnsContractShapeForRequestedEventOnly()
    {
        await using var host = await HostAsync(r => r
            .AddEvent(EventId, "Koncert", Points(4, TransportMode.PublicTransport))
            .AddEvent(OtherEventId, "Mecz", Points(9, TransportMode.Car)));

        var response = await host.Client.GetAsync($"/api/pulse/events/{EventId}");
        var json = await JsonAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.GetProperty("eventId").GetGuid().Should().Be(EventId);
        json.GetProperty("eventName").GetString().Should().Be("Koncert");
        json.GetProperty("participantsCount").GetInt32().Should().Be(4);
        json.GetProperty("modalSplit").GetProperty("publicTransport").GetInt32().Should().Be(4);
        json.GetProperty("alerts").GetArrayLength().Should().Be(0);
    }

    // 21:00 UTC = 23:00 w Warszawie (CEST) -> pozny koniec; 19:30 UTC = 21:30 -> wczesny koniec.
    private static readonly DateTimeOffset LateEnd = new(2026, 9, 25, 21, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EarlyEnd = new(2026, 9, 25, 19, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task EventPulse_ForLateEvent_ReturnsReturnGapAlertMatchingContract()
    {
        await using var host = await HostAsync(r => r
            .AddEvent(EventId, "Nocny Bieg",
                Points(21, TransportMode.PublicTransport).Concat(Points(10, TransportMode.Walking)), LateEnd));

        var json = await JsonAsync(await host.Client.GetAsync($"/api/pulse/events/{EventId}"));

        json.GetProperty("participantsWithoutReturn").GetInt32().Should().Be(21);
        var alert = json.GetProperty("alerts").EnumerateArray().Should().ContainSingle().Subject;
        alert.GetProperty("code").GetString().Should().Be("ReturnGap");
        alert.GetProperty("severity").GetString().Should().Be("Warning");
        alert.GetProperty("message").GetString().Should().Be("21 osob nie ma dogodnego powrotu po 22:00.");
        alert.EnumerateObject().Should().HaveCount(3);
    }

    [Fact]
    public async Task EventPulse_ForEarlyEvent_HasNoReturnGap()
    {
        await using var host = await HostAsync(r => r
            .AddEvent(EventId, "Koncert", Points(21, TransportMode.PublicTransport), EarlyEnd));

        var json = await JsonAsync(await host.Client.GetAsync($"/api/pulse/events/{EventId}"));

        json.GetProperty("participantsWithoutReturn").GetInt32().Should().Be(0);
        json.GetProperty("alerts").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Summary_SumsParticipantsWithoutReturnOfLateEvents()
    {
        await using var host = await HostAsync(r => r
            .AddEvent(EventId, "Nocny Bieg", Points(7, TransportMode.PublicTransport), LateEnd)
            .AddEvent(OtherEventId, "Koncert", Points(5, TransportMode.PublicTransport), EarlyEnd));

        var json = await JsonAsync(await host.Client.GetAsync("/api/pulse/summary"));

        json.GetProperty("participantsWithoutReturn").GetInt32().Should().Be(7);
    }

    [Fact]
    public async Task EventPulse_ForUnknownEvent_Returns404ProblemDetails()
    {
        await using var host = await HostAsync();

        var response = await host.Client.GetAsync($"/api/pulse/events/{EventId}");
        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task EventPulse_WithInvalidEventId_Returns400ProblemDetails(string eventId)
    {
        await using var host = await HostAsync();

        using var response = await host.Client.GetAsync($"/api/pulse/events/{eventId}");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(9, 0)]
    [InlineData(10, 1)]
    [InlineData(11, 1)]
    public async Task Hexagons_AreReturnedOnlyFromTenParticipants(int participants, int expectedFeatures)
    {
        await using var host = await HostAsync(r => r.AddEvent(EventId, "Koncert", Points(participants, TransportMode.Walking)));

        var response = await host.Client.GetAsync($"/api/pulse/hexagons?eventId={EventId}");
        var json = await JsonAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/geo+json");
        json.GetProperty("type").GetString().Should().Be("FeatureCollection");
        json.GetProperty("features").GetArrayLength().Should().Be(expectedFeatures);
    }

    [Fact]
    public async Task Hexagons_FeatureMatchesContractAndIsAnonymous()
    {
        await using var host = await HostAsync(r => r.AddEvent(
            EventId, "Koncert",
            Points(6, TransportMode.PublicTransport).Concat(Points(4, TransportMode.Bike))));

        var response = await host.Client.GetAsync($"/api/pulse/hexagons?eventId={EventId}");
        var body = await response.Content.ReadAsStringAsync();
        var feature = JsonDocument.Parse(body).RootElement.GetProperty("features")[0];

        feature.GetProperty("type").GetString().Should().Be("Feature");
        feature.GetProperty("id").GetString().Should().StartWith("hex-");
        var properties = feature.GetProperty("properties");
        properties.GetProperty("participants").GetInt32().Should().Be(10);
        properties.GetProperty("publicTransport").GetInt32().Should().Be(6);
        properties.GetProperty("bike").GetInt32().Should().Be(4);

        var geometry = feature.GetProperty("geometry");
        geometry.GetProperty("type").GetString().Should().Be("Polygon");
        var ring = geometry.GetProperty("coordinates")[0];
        ring.GetArrayLength().Should().Be(7);
        ring[0].GetRawText().Should().Be(ring[6].GetRawText());
        ring[0][0].GetDouble().Should().BeInRange(18.9, 19.2);
        ring[0][1].GetDouble().Should().BeInRange(49.7, 49.9);

        body.Should().NotContainEquivalentOf("userId");
        body.Should().NotContain(Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Hexagons_FilterByEventId()
    {
        await using var host = await HostAsync(r => r
            .AddEvent(EventId, "A", Points(10, TransportMode.Walking))
            .AddEvent(OtherEventId, "B", Points(2, TransportMode.Walking)));

        var first = await JsonAsync(await host.Client.GetAsync($"/api/pulse/hexagons?eventId={EventId}"));
        var second = await JsonAsync(await host.Client.GetAsync($"/api/pulse/hexagons?eventId={OtherEventId}"));

        first.GetProperty("features").GetArrayLength().Should().Be(1);
        second.GetProperty("features").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Hexagons_ForUnknownEvent_Returns404()
    {
        await using var host = await HostAsync();

        var response = await host.Client.GetAsync($"/api/pulse/hexagons?eventId={EventId}");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api/pulse/hexagons")]
    [InlineData("/api/pulse/hexagons?eventId=00000000-0000-0000-0000-000000000000")]
    [InlineData("/api/pulse/hexagons?eventId=not-a-guid")]
    public async Task Hexagons_WithMissingOrInvalidEventId_Returns400(string url)
    {
        await using var host = await HostAsync();

        var response = await host.Client.GetAsync(url);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }
}
