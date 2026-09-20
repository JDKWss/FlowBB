using System.Net;
using System.Text;
using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;
using FlowBB.Infrastructure.Routing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowBB.Api.IntegrationTests.Routing;

public sealed class RoutingServiceClientTests
{
    [Fact]
    public async Task CalculateAsync_MapsNormalizedRoadRoute()
    {
        using var httpClient = ClientReturning(HttpStatusCode.OK, """
            {
              "mode":"Walking",
              "distanceMeters":1234.5,
              "durationSeconds":900,
              "geometry":{"type":"LineString","coordinates":[[19.03,49.81],[19.04,49.82]]},
              "steps":[]
            }
            """);
        var client = new RoutingServiceClient(httpClient, NullLogger<RoutingServiceClient>.Instance);

        var result = await client.CalculateAsync(Request(TransportMode.Walking));

        result.Mode.Should().Be("Walking");
        result.DistanceMeters.Should().Be(1234.5);
        result.Coordinates.Should().HaveCount(2);
    }

    [Fact]
    public async Task CalculateAsync_RejectsPublicTransportWithoutCallingService()
    {
        using var httpClient = ClientReturning(HttpStatusCode.OK, "{}");
        var client = new RoutingServiceClient(httpClient, NullLogger<RoutingServiceClient>.Instance);

        var action = () => client.CalculateAsync(Request(TransportMode.PublicTransport));

        var exception = await action.Should().ThrowAsync<RoutingServiceException>();
        exception.Which.Failure.Should().Be(RoutingServiceFailure.UnsupportedMode);
    }

    [Fact]
    public async Task CalculateAsync_MapsRouteNotFoundWithoutFallback()
    {
        using var httpClient = ClientReturning(
            HttpStatusCode.NotFound,
            """{"code":"ROUTE_NOT_FOUND","message":"No route exists."}""");
        var client = new RoutingServiceClient(httpClient, NullLogger<RoutingServiceClient>.Instance);

        var action = () => client.CalculateAsync(Request(TransportMode.Car));

        var exception = await action.Should().ThrowAsync<RoutingServiceException>();
        exception.Which.Failure.Should().Be(RoutingServiceFailure.RouteNotFound);
    }

    [Fact]
    public async Task CalculateAsync_RejectsMalformedSuccessResponse()
    {
        using var httpClient = ClientReturning(HttpStatusCode.OK, "not-json");
        var client = new RoutingServiceClient(httpClient, NullLogger<RoutingServiceClient>.Instance);

        var action = () => client.CalculateAsync(Request(TransportMode.Walking));

        var exception = await action.Should().ThrowAsync<RoutingServiceException>();
        exception.Which.Failure.Should().Be(RoutingServiceFailure.InvalidResponse);
    }

    [Fact]
    public async Task CalculateAsync_RejectsOutOfRangeGeometry()
    {
        using var httpClient = ClientReturning(HttpStatusCode.OK, """
            {
              "mode":"Walking",
              "distanceMeters":1234.5,
              "durationSeconds":900,
              "geometry":{"type":"LineString","coordinates":[[181,49.81],[19.04,49.82]]},
              "steps":[]
            }
            """);
        var client = new RoutingServiceClient(httpClient, NullLogger<RoutingServiceClient>.Instance);

        var action = () => client.CalculateAsync(Request(TransportMode.Walking));

        var exception = await action.Should().ThrowAsync<RoutingServiceException>();
        exception.Which.Failure.Should().Be(RoutingServiceFailure.InvalidResponse);
    }

    private static RouteRequest Request(TransportMode mode) => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        DateTimeOffset.Parse("2026-09-25T19:00:00+02:00"),
        DateTimeOffset.Parse("2026-09-25T21:00:00+02:00"),
        new GeoPoint(49.82245, 19.04431),
        new GeoPoint(49.81272, 19.03384),
        mode);

    private static HttpClient ClientReturning(HttpStatusCode statusCode, string json) => new(
        new StubHandler(statusCode, json))
    {
        BaseAddress = new Uri("http://routing:8000/")
    };

    private sealed class StubHandler(HttpStatusCode statusCode, string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }
}
