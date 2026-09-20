using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FlowBB.Api.IntegrationTests.Infrastructure;
using FlowBB.Application.Abstractions.Routing;
using FlowBB.Application.AirQuality;
using FlowBB.Application.Pulse;
using FlowBB.Application.Routing;
using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;
using FluentAssertions;

namespace FlowBB.Api.IntegrationTests.Contracts;

public sealed class ContractFixturesTests
{
    private const double PulseOriginLatitude = 49.8225;
    private const double PulseOriginLongitude = 19.0444;

    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherEventId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private sealed record AirQualityRules(
        IReadOnlyList<string> ResponseProperties,
        IReadOnlyList<string> StationProperties,
        IReadOnlyList<string> MeasurementProperties,
        IReadOnlyList<string> AlertProperties,
        IReadOnlyList<string> QualityLevels,
        IReadOnlyList<string> Statuses,
        IReadOnlyList<string> Sources,
        IReadOnlyList<string> AlertSeverities,
        string Unit);

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
    public async Task RoadRouteFixture_MatchesRuntimeShape()
    {
        var origin = new AttendanceOrigin(new GeoPoint(49.798, 19.08), TransportMode.Walking);
        await using var host = await RoutingTestHost.StartAsync(
            RoutingTestHost.CreateEvent(), origin, new FixedRoadPlanner());

        using var response = await host.Client.GetAsync(
            $"/api/events/{RoutingTestHost.EventId}/route?userId={RoutingTestHost.AttendingUserId}");

        await AssertSuccessFixtureAsync("route-road.json", response);
    }

    [Fact]
    public async Task CreateEventFixtures_RequestIsAcceptedAndResponseMatchesRuntimeShape()
    {
        await using var host = await EventsTestHost.StartAsync(FakeEventRepository.WithDemoEvents());
        using var request = new StringContent(
            await File.ReadAllTextAsync(FixturePath("create-event-request.json")), Encoding.UTF8, "application/json");

        using var response = await host.Client.PostAsync("/api/events", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        await AssertSuccessFixtureAsync("event-created.json", response);
    }

    [Fact]
    public async Task AirQualityFixture_MatchesSupportedCanonicalOpenApiConstraints()
    {
        var contract = await ReadContractAsync();
        var rules = ReadAirQualityRules(contract);
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(FixturePath("air-quality.json")));

        ValidateAirQualityFixture(fixture.RootElement, rules);

        fixture.RootElement.GetProperty("pm25").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task AirQualityContract_DefinesPlannedEndpointAndStableEnums()
    {
        var contract = await ReadContractAsync();
        var rules = ReadAirQualityRules(contract);

        contract.Should().Contain("  /api/events/{eventId}/air-quality:");
        contract.Should().Contain("      x-runtime-status: planned\n      operationId: getEventAirQuality");
        rules.QualityLevels.Should().Equal(Enum.GetNames<AirQualityLevel>());
        rules.Statuses.Should().Equal("Fresh", "Stale", "Fallback");
        rules.Sources.Should().Equal("Gios", "Demo");
        foreach (var nullableProperty in new[] { "pm10", "pm25", "no2", "o3", "alert" })
        {
            contract.Should().Contain($"        {nullableProperty}:\n          nullable: true");
        }
    }

    [Fact]
    public async Task AirQualityFixture_WithUnknownEnum_FailsContractValidation()
    {
        var contract = await ReadContractAsync();
        var rules = ReadAirQualityRules(contract);
        var malformedJson = (await File.ReadAllTextAsync(FixturePath("air-quality.json")))
            .Replace("\"Good\"", "\"Excellent\"", StringComparison.Ordinal);
        using var fixture = JsonDocument.Parse(malformedJson);

        var action = () => ValidateAirQualityFixture(fixture.RootElement, rules);

        action.Should().Throw<InvalidDataException>().WithMessage("*qualityLevel*");
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

    /// <summary>Zwraca stala trase drogowa z geometria, tak jak zrobilby to planer korzystajacy z uslugi routingu.</summary>
    private sealed class FixedRoadPlanner : IRoutePlanner
    {
        private static readonly RouteGeometry There = new(
            [new RouteCoordinate(19.08, 49.798), new RouteCoordinate(19.062, 49.81), new RouteCoordinate(19.0443, 49.8224)]);

        private static readonly RouteGeometry Back = new(
            [new RouteCoordinate(19.0443, 49.8224), new RouteCoordinate(19.062, 49.81), new RouteCoordinate(19.08, 49.798)]);

        public Task<RoutePlan> PlanAsync(RouteRequest request, CancellationToken cancellationToken = default)
        {
            var departure = new DateTimeOffset(2026, 9, 25, 16, 2, 0, TimeSpan.Zero);
            var outbound = Journey(departure, There);
            var returns = Journey(departure.AddHours(3).AddMinutes(38), Back);
            return Task.FromResult(new RoutePlan(PlannerSource.RoadRouting, outbound, [returns], returnGap: false));
        }

        private static JourneyOption Journey(DateTimeOffset departure, RouteGeometry geometry) => new(
            58,
            departure,
            departure.AddMinutes(58),
            [new RouteStep(RouteStepType.Walk, "Idz na miejsce wydarzenia.", 58)],
            distanceMeters: 4620.5,
            geometry: geometry);
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

    // The solution has no general OpenAPI instance-validation package. This focused offline validator reads
    // required fields, enum values and the unit from the canonical schema, then enforces every #94 constraint.
    private static AirQualityRules ReadAirQualityRules(string contract)
    {
        var response = GetClosedSchemaBlock(contract, "AirQualityResponse");
        var station = GetClosedSchemaBlock(contract, "AirQualityStation");
        var measurement = GetClosedSchemaBlock(contract, "AirQualityMeasurement");
        var alert = GetClosedSchemaBlock(contract, "AirQualityAlert");

        return new AirQualityRules(
            ReadList(response, "required"),
            ReadList(station, "required"),
            ReadList(measurement, "required"),
            ReadList(alert, "required"),
            ReadList(GetSchemaBlock(contract, "AirQualityLevel"), "enum"),
            ReadList(GetSchemaBlock(contract, "AirQualityStatus"), "enum"),
            ReadList(GetSchemaBlock(contract, "AirQualitySource"), "enum"),
            ReadList(alert, "enum"),
            ReadList(measurement, "enum").Single());
    }

    private static void ValidateAirQualityFixture(JsonElement root, AirQualityRules rules)
    {
        RequireExactProperties(root, rules.ResponseProperties, "AirQualityResponse");
        Require(Guid.TryParse(root.GetProperty("eventId").GetString(), out var eventId) && eventId != Guid.Empty, "eventId must be a non-empty UUID");
        ValidateStation(root.GetProperty("station"), rules);
        ValidateTimestamp(root.GetProperty("measuredAt"));
        ValidateEnum(root, "qualityLevel", rules.QualityLevels);
        ValidateEnum(root, "status", rules.Statuses);
        ValidateEnum(root, "source", rules.Sources);

        var pollutants = new[] { "pm10", "pm25", "no2", "o3" };
        foreach (var pollutant in pollutants)
        {
            ValidateMeasurement(root.GetProperty(pollutant), pollutant, rules);
        }

        Require(pollutants.Any(name => root.GetProperty(name).ValueKind != JsonValueKind.Null), "at least one pollutant is required");
        ValidateAlert(root.GetProperty("alert"), rules);
    }

    private static void ValidateStation(JsonElement station, AirQualityRules rules)
    {
        RequireExactProperties(station, rules.StationProperties, "station");
        Require(!string.IsNullOrWhiteSpace(station.GetProperty("name").GetString()), "station.name is required");
        RequireNonNegativeNumber(station.GetProperty("distanceMeters"), "station.distanceMeters");
    }

    private static void ValidateTimestamp(JsonElement measuredAt)
    {
        var value = measuredAt.GetString();
        var hasExplicitOffset = value is { Length: >= 6 } && value[^6] is '+' or '-' && value[^3] == ':';
        Require(
            hasExplicitOffset && DateTimeOffset.TryParse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out _),
            "measuredAt must be ISO 8601 with an explicit offset");
    }

    private static void ValidateMeasurement(JsonElement measurement, string propertyName, AirQualityRules rules)
    {
        if (measurement.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        RequireExactProperties(measurement, rules.MeasurementProperties, propertyName);
        RequireNonNegativeNumber(measurement.GetProperty("value"), $"{propertyName}.value");
        Require(measurement.GetProperty("unit").GetString() == rules.Unit, $"{propertyName}.unit must be {rules.Unit}");
    }

    private static void ValidateAlert(JsonElement alert, AirQualityRules rules)
    {
        if (alert.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        RequireExactProperties(alert, rules.AlertProperties, "alert");
        ValidateEnum(alert, "severity", rules.AlertSeverities);
        Require(!string.IsNullOrWhiteSpace(alert.GetProperty("message").GetString()), "alert.message is required");
    }

    private static void ValidateEnum(JsonElement parent, string propertyName, IReadOnlyList<string> allowedValues)
    {
        var value = parent.GetProperty(propertyName).GetString();
        Require(value is not null && allowedValues.Contains(value, StringComparer.Ordinal), $"{propertyName} has an unsupported enum value");
    }

    private static void RequireNonNegativeNumber(JsonElement element, string propertyName)
    {
        Require(element.TryGetDouble(out var value) && double.IsFinite(value) && value >= 0, $"{propertyName} must be finite and non-negative");
    }

    private static void RequireExactProperties(JsonElement element, IReadOnlyList<string> expected, string schemaName)
    {
        Require(element.ValueKind == JsonValueKind.Object, $"{schemaName} must be an object");
        var actual = element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        Require(actual.SetEquals(expected), $"{schemaName} must contain exactly the required OpenAPI properties");
    }

    private static string GetClosedSchemaBlock(string contract, string schemaName)
    {
        var block = GetSchemaBlock(contract, schemaName);
        Require(block.Contains("additionalProperties: false", StringComparison.Ordinal), $"{schemaName} must reject additional properties");
        return block;
    }

    private static string GetSchemaBlock(string contract, string schemaName)
    {
        var lines = contract.Split('\n');
        var header = $"    {schemaName}:";
        var start = Array.FindIndex(lines, line => line.TrimEnd('\r') == header);
        Require(start >= 0, $"OpenAPI schema {schemaName} was not found");

        var end = Array.FindIndex(lines, start + 1, IsSchemaHeader);
        end = end < 0 ? lines.Length : end;
        return string.Join('\n', lines[start..end]);
    }

    private static bool IsSchemaHeader(string line) =>
        line.Length > 4 &&
        line.StartsWith("    ", StringComparison.Ordinal) &&
        line[4] != ' ' &&
        line.TrimEnd('\r').EndsWith(':');

    private static IReadOnlyList<string> ReadList(string schemaBlock, string keyword)
    {
        var lines = schemaBlock.Split('\n');
        var index = Array.FindIndex(lines, line => line.TrimStart().StartsWith($"{keyword}:", StringComparison.Ordinal));
        Require(index >= 0, $"{keyword} was not found in an OpenAPI schema");
        var declaration = lines[index].Trim();

        if (declaration.Contains('['))
        {
            return declaration[(declaration.IndexOf('[') + 1)..declaration.LastIndexOf(']')]
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim('"'))
                .ToArray();
        }

        return lines[(index + 1)..]
            .TakeWhile(line => line.TrimStart().StartsWith("- ", StringComparison.Ordinal))
            .Select(line => line.Trim()[2..].Trim('"'))
            .ToArray();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }

    private static string FixturePath(string fixtureName) =>
        Path.Combine(AppContext.BaseDirectory, "Contracts", "Fixtures", fixtureName);

    // Na Windows (core.autocrlf=true) plik ma zakonczenia CRLF, a testy porownuja kontrakt z tekstem zawierajacym "\n".
    // Normalizacja sprawia, ze wynik nie zalezy od systemu, w ktorym repozytorium zostalo wyciagniete.
    private static async Task<string> ReadContractAsync() =>
        (await File.ReadAllTextAsync(ContractPath())).ReplaceLineEndings("\n");

    private static string ContractPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "contracts", "openapi.yaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("contracts/openapi.yaml was not found above the test output directory.");
    }
}
