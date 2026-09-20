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

    private const string ValidStart = "\"startAt\":\"2026-09-25T19:00:00+02:00\"";
    private const string ValidLocation = "\"location\":{\"latitude\":49.82,\"longitude\":19.04}";

    private static string EventBody(string? category = "Community", string? end = null, string? location = ValidLocation) =>
        "{\"name\":\"Smoke event\",\"description\":\"d\",\"venueName\":\"Bielsko-Biala\"," + ValidStart +
        (category is null ? string.Empty : $",\"category\":\"{category}\"") +
        (end is null ? string.Empty : $",\"endAt\":\"{end}\"") +
        (location is null ? string.Empty : "," + location) + "}";

    // Poprawne wydarzenie (201) nie jest testowane na prawdziwym stosie: API nie ma operacji usuwania, a testy smoke sprzataja
    // po sobie. Tworzenie pokrywaja testy z fake'ami oraz Neo4jEventWriterTests. Tu tylko odrzucenia, ktore niczego nie zapisuja.
    [SmokeTheory]
    [InlineData("{}", "application/json")]
    [InlineData("not json", "application/json")]
    [InlineData("", "application/json")]
    [InlineData("MISSING_CATEGORY", "application/json")]
    [InlineData("BAD_CATEGORY", "application/json")]
    [InlineData("MISSING_LOCATION", "application/json")]
    [InlineData("BAD_LATITUDE", "application/json")]
    [InlineData("END_BEFORE_START", "application/json")]
    public async Task CreateEvent_WithInvalidRequest_Returns400AndCreatesNothing(string body, string contentType)
    {
        var before = (await Api.GetJsonAsync("/api/events")).GetArrayLength();
        var payload = body switch
        {
            "MISSING_CATEGORY" => EventBody(category: null),
            "BAD_CATEGORY" => EventBody(category: "NotACategory"),
            "MISSING_LOCATION" => EventBody(location: null),
            "BAD_LATITUDE" => EventBody(location: "\"location\":{\"latitude\":91,\"longitude\":19.04}"),
            "END_BEFORE_START" => EventBody(end: "2026-09-25T18:00:00+02:00"),
            _ => body
        };

        using var response = await Api.PostRawAsync("/api/events", payload, contentType);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
        (await Api.GetJsonAsync("/api/events")).GetArrayLength().Should().Be(before, "a rejected request must not create an event");
    }

    // Znana rozbieznosc (#120): POST /api/events zwraca 415 z pustym body zamiast 400 ProblemDetails, jak pozostale endpointy i
    // kontrakt. Test opisuje oczekiwane zachowanie; Skip zdejmuje poprawka #120.
    [SmokeFact(Skip = "Known bug #120: createEvent returns 415 with an empty body instead of 400 ProblemDetails for a non-JSON Content-Type.")]
    public async Task CreateEvent_WithNonJsonContentType_Returns400()
    {
        using var response = await Api.PostRawAsync("/api/events", EventBody(), "text/plain");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
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
