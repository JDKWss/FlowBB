using System.Globalization;
using System.Net;
using System.Text.Json;
using FlowBB.Api.IntegrationTests.Endpoints;
using FlowBB.Api.IntegrationTests.Infrastructure;
using FlowBB.Application.Routing;
using FlowBB.Domain.Common;
using FlowBB.Infrastructure.Routing;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace FlowBB.Api.IntegrationTests.Routing;

public sealed class DemoRoutePlannerEndToEndTests
{
    private static readonly GeoPoint Origin = new(49.798, 19.08);

    private static string RouteUrl =>
        $"/api/events/{RoutingTestHost.EventId}/route?userId={RoutingTestHost.AttendingUserId}";

    [Theory]
    [InlineData(TransportMode.Walking, "Walk")]
    [InlineData(TransportMode.PublicTransport, "Transit")]
    [InlineData(TransportMode.Bike, "Bike")]
    [InlineData(TransportMode.Car, "Car")]
    public async Task GetRoute_ForRoutableMode_ReturnsRepeatableDemoResponse(
        TransportMode mode,
        string expectedStepType)
    {
        var attendance = new AttendanceOrigin(Origin, mode);
        await using var host = await RoutingTestHost.StartAsync(RoutingTestHost.CreateEvent(), attendance);

        using var firstResponse = await host.Client.GetAsync(RouteUrl);
        using var secondResponse = await host.Client.GetAsync(RouteUrl);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondBody.Should().Be(firstBody);
        using var document = JsonDocument.Parse(firstBody);
        AssertRouteResponse(document.RootElement, expectedStepType);
    }

    [Fact]
    public async Task GetRoute_WhenModeIsUnknown_ReturnsRepeatableBadRequestWithoutFallback()
    {
        var attendance = new AttendanceOrigin(Origin, TransportMode.Unknown);
        await using var host = await RoutingTestHost.StartAsync(RoutingTestHost.CreateEvent(), attendance);

        using var first = await host.Client.GetAsync(RouteUrl);
        using var second = await host.Client.GetAsync(RouteUrl);

        await ProblemResponseAssertions.AssertAsync(first, HttpStatusCode.BadRequest);
        await ProblemResponseAssertions.AssertAsync(second, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetRoute_WhenOriginEqualsEventPoint_ReturnsValidMinimumDurationJourney()
    {
        var existingEvent = RoutingTestHost.CreateEvent();
        var attendance = new AttendanceOrigin(existingEvent.Location, TransportMode.Walking);
        await using var host = await RoutingTestHost.StartAsync(existingEvent, attendance);

        using var response = await host.Client.GetAsync(RouteUrl);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        document.RootElement.GetProperty("outbound").GetProperty("durationMinutes").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GetRoute_LogsFallbackEventIdWithoutPrivateRouteData()
    {
        var logger = new ListLogger<DemoRoutePlanner>();
        var planner = new DemoRoutePlanner(logger);
        var attendance = new AttendanceOrigin(Origin, TransportMode.Bike);
        await using var host = await RoutingTestHost.StartAsync(RoutingTestHost.CreateEvent(), attendance, planner);

        using var response = await host.Client.GetAsync(RouteUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var entry = logger.Entries.Should().ContainSingle().Which;
        entry.Level.Should().Be(LogLevel.Information);
        entry.Message.Should().Contain(RoutingTestHost.EventId.ToString());
        entry.Message.Should().NotContain(RoutingTestHost.AttendingUserId.ToString());
        entry.Message.Should().NotContain(Origin.Latitude.ToString(CultureInfo.InvariantCulture));
        entry.Message.Should().NotContain(Origin.Longitude.ToString(CultureInfo.InvariantCulture));
    }

    private static void AssertRouteResponse(JsonElement root, string expectedStepType)
    {
        root.GetProperty("eventId").GetGuid().Should().Be(RoutingTestHost.EventId);
        root.GetProperty("userId").GetGuid().Should().Be(RoutingTestHost.AttendingUserId);
        root.GetProperty("plannerSource").GetString().Should().Be("Demo");
        root.GetProperty("returnGap").GetBoolean().Should().BeFalse();
        root.GetProperty("returns").GetArrayLength().Should().BeGreaterThan(0);

        var outbound = root.GetProperty("outbound");
        outbound.GetProperty("durationMinutes").GetInt32().Should().BeGreaterThan(0);
        outbound.GetProperty("departureAt").GetDateTimeOffset()
            .Should().BeBefore(outbound.GetProperty("arrivalAt").GetDateTimeOffset());
        outbound.GetProperty("steps").EnumerateArray()
            .Should().Contain(step => step.GetProperty("type").GetString() == expectedStepType);
    }
}
