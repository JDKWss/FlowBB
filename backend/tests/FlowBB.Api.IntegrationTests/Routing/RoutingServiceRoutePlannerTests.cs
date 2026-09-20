using System.Net;
using System.Text;
using System.Text.Json;
using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;
using FlowBB.Infrastructure.Routing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowBB.Api.IntegrationTests.Routing;

public sealed class RoutingServiceRoutePlannerTests
{
    private static readonly GeoPoint EventLocation = new(49.82245, 19.04431);
    private static readonly GeoPoint ResidentOrigin = new(49.81272, 19.03384);

    [Fact]
    public async Task PlanAsync_RequestsOutboundFromResidentToEventAndReturnFromEventToResident()
    {
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://routing:8000/") };
        var planner = new RoutingServiceRoutePlanner(
            new RoutingServiceClient(httpClient, NullLogger<RoutingServiceClient>.Instance));
        var request = new RouteRequest(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            DateTimeOffset.Parse("2026-09-25T19:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-25T21:00:00+02:00"),
            destination: EventLocation,
            origin: ResidentOrigin,
            TransportMode.Walking);

        await planner.PlanAsync(request);

        handler.Bodies.Should().HaveCount(2);
        var outbound = Endpoints(handler.Bodies[0]);
        outbound.Origin.Should().Be(ResidentOrigin);
        outbound.Destination.Should().Be(EventLocation);
        var back = Endpoints(handler.Bodies[1]);
        back.Origin.Should().Be(EventLocation);
        back.Destination.Should().Be(ResidentOrigin);
    }

    private static (GeoPoint Origin, GeoPoint Destination) Endpoints(string body)
    {
        using var document = JsonDocument.Parse(body);
        return (Point(document.RootElement.GetProperty("origin")), Point(document.RootElement.GetProperty("destination")));
    }

    private static GeoPoint Point(JsonElement element) =>
        new(element.GetProperty("latitude").GetDouble(), element.GetProperty("longitude").GetDouble());

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"mode":"Walking","distanceMeters":1200,"durationSeconds":900,
                     "geometry":{"type":"LineString","coordinates":[[19.03,49.81],[19.04,49.82]]},"steps":[]}
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
