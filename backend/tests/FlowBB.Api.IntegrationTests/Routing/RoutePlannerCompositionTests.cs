using System.Net;
using System.Text;
using System.Text.Json;
using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;
using FlowBB.Infrastructure.Routing;
using FlowBB.Infrastructure.Routing.Mzk;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowBB.Api.IntegrationTests.Routing;

public sealed class RoutePlannerCompositionTests
{
    private static readonly GeoPoint Origin = new(49.81272, 19.03384);
    private static readonly GeoPoint Destination = new(49.82245, 19.04431);

    [Theory]
    [InlineData(TransportMode.Walking, "Walk")]
    [InlineData(TransportMode.Bike, "Bike")]
    [InlineData(TransportMode.Car, "Car")]
    public async Task RoadPlanner_CalculatesOutboundAndReturnSeparately(
        TransportMode mode,
        string expectedStepType)
    {
        var handler = new RecordingHandler(new Queue<HttpResponseMessage>([
            Success(mode, 1625.6, 1220, [[19.03384, 49.81272], [19.04, 49.818], [19.04431, 49.82245]]),
            Success(mode, 1700.2, 1280, [[19.04431, 49.82245], [19.041, 49.817], [19.03384, 49.81272]])
        ]));
        var planner = RoadPlanner(handler);

        var result = await planner.PlanAsync(Request(mode));

        result.Source.Should().Be(PlannerSource.RoadRouting);
        result.Outbound.DistanceMeters.Should().Be(1625.6);
        result.Returns.Single().DistanceMeters.Should().Be(1700.2);
        result.Outbound.Geometry!.Coordinates.Should().HaveCount(3);
        result.Outbound.Steps.Single().Type.ToString().Should().Be(expectedStepType);
        handler.RequestBodies.Should().HaveCount(2);
        Coordinate(handler.RequestBodies[0], "origin").Should().Equal(49.81272, 19.03384);
        Coordinate(handler.RequestBodies[1], "origin").Should().Equal(49.82245, 19.04431);
    }

    [Fact]
    public async Task Composite_PublicTransport_UsesTheTimetableWithoutCallingRoadService()
    {
        var handler = new RecordingHandler(new Queue<HttpResponseMessage>());
        var composite = Composite(handler, fallback: true);

        var result = await composite.PlanAsync(Request(TransportMode.PublicTransport));

        result.Source.Should().Be(PlannerSource.MzkTimetable);
        result.Outbound.Geometry.Should().BeNull();
        result.Outbound.Stops.Should().HaveCount(2);
        handler.RequestBodies.Should().BeEmpty();
    }

    [Fact]
    public async Task Composite_PublicTransport_FallsBackToDemoWhenTheTimetableCannotPlan()
    {
        var handler = new RecordingHandler(new Queue<HttpResponseMessage>());
        var composite = Composite(handler, fallback: true);
        var farAway = new GeoPoint(50.0647, 19.9450);
        var request = new RouteRequest(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            DateTimeOffset.Parse("2026-09-25T19:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-25T21:30:00+02:00"),
            Destination,
            farAway,
            TransportMode.PublicTransport);

        var result = await composite.PlanAsync(request);

        result.Source.Should().Be(PlannerSource.Demo);
        result.Outbound.Stops.Should().BeNull();
        handler.RequestBodies.Should().BeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "GRAPH_NOT_READY", true, PlannerSource.Demo)]
    [InlineData(HttpStatusCode.NotFound, "ROUTE_NOT_FOUND", true, null)]
    [InlineData(HttpStatusCode.OK, null, true, null)]
    public async Task Composite_FallsBackOnlyForTransientInfrastructureFailures(
        HttpStatusCode status,
        string? errorCode,
        bool fallback,
        PlannerSource? expectedSource)
    {
        var body = status == HttpStatusCode.OK
            ? "not-json"
            : $$"""{"code":"{{errorCode}}","message":"failure"}""";
        var handler = new RecordingHandler(new Queue<HttpResponseMessage>([new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        }]));
        var composite = Composite(handler, fallback);

        var action = () => composite.PlanAsync(Request(TransportMode.Walking));

        if (expectedSource.HasValue)
        {
            (await action()).Source.Should().Be(expectedSource.Value);
        }
        else
        {
            await action.Should().ThrowAsync<RoutingServiceException>();
        }
    }

    private static CompositeRoutePlanner Composite(RecordingHandler handler, bool fallback) => new(
        new DemoRoutePlanner(),
        TimetablePlanner(),
        RoadPlanner(handler),
        new RoutingServiceOptions(new Uri("http://routing:8000"), TimeSpan.FromSeconds(3), fallback),
        NullLogger<CompositeRoutePlanner>.Instance);

    // Prawdziwy rozklad z zasobow osadzonych w assembly: te testy sprawdzaja wybor planera, nie godziny.
    private static TimetableFallbackRoutePlanner TimetablePlanner() => new(
        new MzkTimetableRoutePlanner(new MzkTimetableProvider(NullLogger<MzkTimetableProvider>.Instance)),
        new DemoRoutePlanner(),
        NullLogger<TimetableFallbackRoutePlanner>.Instance);

    private static RoutingServiceRoutePlanner RoadPlanner(RecordingHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://routing:8000/") };
        return new RoutingServiceRoutePlanner(
            new RoutingServiceClient(httpClient, NullLogger<RoutingServiceClient>.Instance));
    }

    private static RouteRequest Request(TransportMode mode) => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        DateTimeOffset.Parse("2026-09-25T19:00:00+02:00"),
        DateTimeOffset.Parse("2026-09-25T21:30:00+02:00"),
        Destination,
        Origin,
        mode);

    private static HttpResponseMessage Success(
        TransportMode mode,
        double distance,
        int duration,
        double[][] coordinates) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                mode = mode.ToString(),
                distanceMeters = distance,
                durationSeconds = duration,
                geometry = new { type = "LineString", coordinates },
                steps = Array.Empty<object>()
            }), Encoding.UTF8, "application/json")
        };

    private static double[] Coordinate(string json, string property)
    {
        using var document = JsonDocument.Parse(json);
        var coordinate = document.RootElement.GetProperty(property);
        return [coordinate.GetProperty("latitude").GetDouble(), coordinate.GetProperty("longitude").GetDouble()];
    }

    private sealed class RecordingHandler(Queue<HttpResponseMessage> responses) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return responses.Dequeue();
        }
    }
}
