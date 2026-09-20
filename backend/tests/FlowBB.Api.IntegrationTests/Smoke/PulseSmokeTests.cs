using System.Net;
using System.Text.Json;
using FlowBB.Api.IntegrationTests.Endpoints;
using FluentAssertions;

namespace FlowBB.Api.IntegrationTests.Smoke;

[Collection(SmokeCollection.Name)]
public sealed class PulseSmokeTests : SmokeTestBase
{
    private static int Sum(JsonElement split) =>
        new[] { "publicTransport", "walking", "bike", "car", "unknown" }.Sum(name => split.GetProperty(name).GetInt32());

    [SmokeFact]
    public async Task EventPulse_HasConsistentAggregatesAndMatchesTheEventList()
    {
        var pulse = await Api.PulseAsync(SmokeSeed.Concert);
        var events = await Api.GetJsonAsync("/api/events");

        var count = pulse.GetProperty("participantsCount").GetInt32();
        Sum(pulse.GetProperty("modalSplit")).Should().Be(count);
        pulse.GetProperty("eventId").GetGuid().Should().Be(SmokeSeed.Concert);
        pulse.GetProperty("alerts").ValueKind.Should().Be(JsonValueKind.Array);
        events.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == SmokeSeed.Concert)
            .GetProperty("participantsCount").GetInt32().Should().Be(count);
    }

    [SmokeFact]
    public async Task EventPulse_WithUnknownEvent_Returns404()
    {
        using var response = await Api.GetAsync($"/api/pulse/events/{SmokeSeed.Unknown}");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.NotFound);
    }

    [SmokeFact]
    public async Task EventPulse_WithInvalidId_Returns400()
    {
        using var response = await Api.GetAsync("/api/pulse/events/not-a-guid");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }

    [SmokeFact]
    public async Task Summary_AggregatesAllEvents()
    {
        var summary = await Api.GetJsonAsync("/api/pulse/summary");
        var events = await Api.GetJsonAsync("/api/events");

        var total = events.EnumerateArray().Sum(item => item.GetProperty("participantsCount").GetInt32());
        summary.GetProperty("eventsCount").GetInt32().Should().Be(events.GetArrayLength());
        summary.GetProperty("participantsCount").GetInt32().Should().Be(total);
        Sum(summary.GetProperty("modalSplit")).Should().Be(total);
    }

    [SmokeFact]
    public async Task Hexagons_AreGeoJsonThatRespectsThePrivacyRules()
    {
        using var response = await Api.GetAsync($"/api/pulse/hexagons?eventId={SmokeSeed.Concert}");
        var raw = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(raw);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/geo+json");
        document.RootElement.GetProperty("type").GetString().Should().Be("FeatureCollection");
        raw.Should().NotContainEquivalentOf("userId", "PULSE never exposes user identifiers");
        AssertEveryCellIsPrivacySafe(document.RootElement.GetProperty("features"));
    }

    private static void AssertEveryCellIsPrivacySafe(JsonElement features)
    {
        features.GetArrayLength().Should().BeGreaterThan(0, "the demo map must not be empty");
        foreach (var feature in features.EnumerateArray())
        {
            var properties = feature.GetProperty("properties");
            properties.GetProperty("participants").GetInt32().Should().BeGreaterThanOrEqualTo(10);
            properties.EnumerateObject().Select(property => property.Name)
                .Should().BeEquivalentTo("participants", "publicTransport", "walking", "bike", "car");

            var ring = feature.GetProperty("geometry").GetProperty("coordinates")[0];
            ring[0].GetRawText().Should().Be(ring[ring.GetArrayLength() - 1].GetRawText(), "the ring must be closed");
        }
    }

    [SmokeTheory]
    [InlineData("")]
    [InlineData("?eventId=not-a-guid")]
    [InlineData("?eventId=00000000-0000-0000-0000-000000000000")]
    public async Task Hexagons_WithMissingOrInvalidEventId_Returns400(string query)
    {
        using var response = await Api.GetAsync($"/api/pulse/hexagons{query}");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }

    [SmokeFact]
    public async Task Hexagons_WithUnknownEvent_Returns404()
    {
        using var response = await Api.GetAsync($"/api/pulse/hexagons?eventId={SmokeSeed.Unknown}");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.NotFound);
    }
}
