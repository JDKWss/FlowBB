using System.Net;
using System.Text.Json;
using FluentAssertions;
using FlowBB.Api.IntegrationTests.Infrastructure;

namespace FlowBB.Api.IntegrationTests.Endpoints.Events;

public class EventsContractTests : IAsyncLifetime
{
    private static readonly string[] AllowedCategories =
        ["Culture", "Sport", "Education", "Community", "Other"];

    private static readonly string[] AllowedSources = ["Demo", "City", "External"];

    private static readonly string[] AllowedTransportModes =
        ["Walking", "PublicTransport", "Bike", "Car", "Unknown"];

    private EventsTestHost _host = null!;

    public async Task InitializeAsync() =>
        _host = await EventsTestHost.StartAsync(FakeEventRepository.WithDemoEvents());

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task GetEvents_WithDemoSeed_ReturnsNonEmptyEventSummariesMatchingContract()
    {
        var client = _host.Client;
        using var response = await client.GetAsync("/api/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = await ReadJsonAsync(response);
        document.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        document.RootElement.GetArrayLength().Should().BeGreaterThan(0);

        foreach (var eventSummary in document.RootElement.EnumerateArray())
        {
            AssertEventSummary(eventSummary);
        }
    }

    [Fact]
    public async Task GetEvent_WithIdFromEventsList_ReturnsMatchingEventDetails()
    {
        var client = _host.Client;
        var eventId = await GetFirstEventIdAsync(client);

        using var response = await client.GetAsync($"/api/events/{eventId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = await ReadJsonAsync(response);
        GetRequiredGuid(document.RootElement, "id").Should().Be(eventId);
        GetRequiredProperty(document.RootElement, "crewAvailable").ValueKind
            .Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);
        AssertTransportModes(GetRequiredProperty(document.RootElement, "availableTransportModes"));
    }

    [Fact]
    public async Task GetEvent_WithUnknownId_ReturnsNotFoundProblemDetails()
    {
        var client = _host.Client;
        var unknownEventId = Guid.NewGuid();

        using var response = await client.GetAsync($"/api/events/{unknownEventId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var contentType = response.Content.Headers.ContentType;
        contentType.Should().NotBeNull();
        contentType!.MediaType.Should().Be("application/problem+json");

        using var document = await ReadJsonAsync(response);
        GetRequiredProperty(document.RootElement, "title").ValueKind.Should().Be(JsonValueKind.String);
        GetRequiredProperty(document.RootElement, "status").GetInt32().Should().Be(404);
    }

    private static void AssertEventSummary(JsonElement eventSummary)
    {
        eventSummary.ValueKind.Should().Be(JsonValueKind.Object);
        GetRequiredGuid(eventSummary, "id");
        AssertRequiredString(eventSummary, "name", allowEmpty: false);
        AssertRequiredString(eventSummary, "description", allowEmpty: true);
        AssertRequiredDateTime(eventSummary, "startAt");
        AssertOptionalDateTime(eventSummary, "endAt");
        AssertRequiredString(eventSummary, "venueName", allowEmpty: false);
        GetRequiredProperty(eventSummary, "category").GetString().Should().BeOneOf(AllowedCategories);
        AssertLocation(GetRequiredProperty(eventSummary, "location"));
        AssertParticipantsCount(GetRequiredProperty(eventSummary, "participantsCount"));
        GetRequiredProperty(eventSummary, "source").GetString().Should().BeOneOf(AllowedSources);
    }

    private static void AssertRequiredString(JsonElement element, string propertyName, bool allowEmpty)
    {
        var property = GetRequiredProperty(element, propertyName);
        property.ValueKind.Should().Be(JsonValueKind.String);

        if (!allowEmpty)
        {
            property.GetString().Should().NotBeNullOrEmpty();
        }
    }

    private static void AssertRequiredDateTime(JsonElement element, string propertyName)
    {
        var property = GetRequiredProperty(element, propertyName);
        property.ValueKind.Should().Be(JsonValueKind.String);
        property.TryGetDateTimeOffset(out _).Should().BeTrue($"{propertyName} must be ISO 8601");
    }

    private static void AssertOptionalDateTime(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null)
        {
            return;
        }

        property.ValueKind.Should().Be(JsonValueKind.String);
        property.TryGetDateTimeOffset(out _).Should().BeTrue($"{propertyName} must be ISO 8601");
    }

    private static void AssertLocation(JsonElement location)
    {
        location.ValueKind.Should().Be(JsonValueKind.Object);
        var latitude = GetRequiredProperty(location, "latitude");
        var longitude = GetRequiredProperty(location, "longitude");

        latitude.ValueKind.Should().Be(JsonValueKind.Number);
        latitude.GetDouble().Should().BeInRange(-90, 90);
        longitude.ValueKind.Should().Be(JsonValueKind.Number);
        longitude.GetDouble().Should().BeInRange(-180, 180);
    }

    private static void AssertParticipantsCount(JsonElement participantsCount)
    {
        participantsCount.ValueKind.Should().Be(JsonValueKind.Number);
        participantsCount.TryGetInt64(out var value).Should().BeTrue("participantsCount must be an integer");
        value.Should().BeGreaterThanOrEqualTo(0);
    }

    private static void AssertTransportModes(JsonElement transportModes)
    {
        transportModes.ValueKind.Should().Be(JsonValueKind.Array);
        var values = transportModes.EnumerateArray().Select(mode => mode.GetString()).ToArray();

        values.Should().OnlyContain(mode => AllowedTransportModes.Contains(mode));
        values.Should().OnlyHaveUniqueItems();
    }

    private static async Task<Guid> GetFirstEventIdAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/events");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = await ReadJsonAsync(response);
        document.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        document.RootElement.GetArrayLength().Should().BeGreaterThan(0);
        return GetRequiredGuid(document.RootElement[0], "id");
    }

    private static Guid GetRequiredGuid(JsonElement element, string propertyName)
    {
        var property = GetRequiredProperty(element, propertyName);
        property.ValueKind.Should().Be(JsonValueKind.String);
        Guid.TryParse(property.GetString(), out var value).Should().BeTrue($"{propertyName} must be a UUID");
        return value;
    }

    private static JsonElement GetRequiredProperty(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out var property).Should()
            .BeTrue($"the response must contain required property '{propertyName}'");
        return property;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
