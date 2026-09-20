using System.Net;
using System.Text.Json;
using FlowBB.Api.IntegrationTests.Endpoints;
using FlowBB.Api.IntegrationTests.Infrastructure;
using FlowBB.Application.Abstractions.Routing;
using FlowBB.Application.Routing;
using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;
using FluentAssertions;

namespace FlowBB.Api.IntegrationTests.Endpoints.Routing;

public class RoutingEndpointsTests
{
    private static readonly AttendanceOrigin PublicTransportFromHome =
        new(new GeoPoint(49.798, 19.08), TransportMode.PublicTransport);

    private static string RouteUrl(string eventId, string? userId) =>
        $"/api/events/{eventId}/route" + (userId is null ? string.Empty : $"?userId={userId}");

    private static string ValidUrl => RouteUrl(RoutingTestHost.EventId.ToString(), RoutingTestHost.AttendingUserId.ToString());

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    [Fact]
    public async Task GetRoute_WithDemoPlanner_ReturnsResponseMatchingContract()
    {
        await using var host = await RoutingTestHost.StartAsync(RoutingTestHost.CreateEvent(), PublicTransportFromHome);

        using var response = await host.Client.GetAsync(ValidUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;
        root.GetProperty("eventId").GetGuid().Should().Be(RoutingTestHost.EventId);
        root.GetProperty("userId").GetGuid().Should().Be(RoutingTestHost.AttendingUserId);
        root.GetProperty("plannerSource").GetString().Should().Be("Demo");
        root.GetProperty("returnGap").GetBoolean().Should().BeFalse();
        root.GetProperty("returns").GetArrayLength().Should().BeGreaterThan(0);
        var stepTypes = root.GetProperty("outbound").GetProperty("steps").EnumerateArray()
            .Select(step => step.GetProperty("type").GetString());
        stepTypes.Should().Equal("Walk", "Wait", "Transit", "Walk");
    }

    [Fact]
    public async Task GetRoute_ReturnsTimesInWarsawZone()
    {
        await using var host = await RoutingTestHost.StartAsync(RoutingTestHost.CreateEvent(), PublicTransportFromHome);

        using var response = await host.Client.GetAsync(ValidUrl);

        using var document = await ReadJsonAsync(response);
        var arrival = document.RootElement.GetProperty("outbound").GetProperty("arrivalAt").GetString();
        arrival.Should().Be("2026-09-25T18:50:00+02:00");
    }

    [Fact]
    public async Task GetRoute_DoesNotExposeCoordinates()
    {
        await using var host = await RoutingTestHost.StartAsync(RoutingTestHost.CreateEvent(), PublicTransportFromHome);

        var body = (await host.Client.GetStringAsync(ValidUrl)).ToLowerInvariant();

        body.Should().NotContain("latitude").And.NotContain("longitude").And.NotContain("origin");
    }

    [Fact]
    public async Task GetRoute_SameRequestTwice_ReturnsIdenticalBody()
    {
        await using var host = await RoutingTestHost.StartAsync(RoutingTestHost.CreateEvent(), PublicTransportFromHome);

        var first = await host.Client.GetStringAsync(ValidUrl);
        var second = await host.Client.GetStringAsync(ValidUrl);

        second.Should().Be(first);
    }

    [Fact]
    public async Task GetRoute_ForLateEventWithPublicTransport_ReturnsReturnGapWithEmptyReturns()
    {
        var lateEnd = new DateTimeOffset(2026, 9, 25, 21, 0, 0, TimeSpan.Zero); // 23:00 w Warszawie
        await using var host = await RoutingTestHost.StartAsync(RoutingTestHost.CreateEvent(lateEnd), PublicTransportFromHome);

        using var response = await host.Client.GetAsync(ValidUrl);

        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("returnGap").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("returns").GetArrayLength().Should().Be(0);
        document.RootElement.GetProperty("outbound").GetProperty("steps").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(TransportMode.Walking, true)]
    [InlineData(TransportMode.Bike, true)]
    [InlineData(TransportMode.Car, true)]
    [InlineData(TransportMode.PublicTransport, false)] // wydarzenie konczy sie o 21:30 lokalnie
    public async Task GetRoute_WhenNoReturnGap_KeepsPlannerReturns(TransportMode mode, bool lateEvent)
    {
        var end = lateEvent
            ? new DateTimeOffset(2026, 9, 25, 21, 0, 0, TimeSpan.Zero)  // 23:00 w Warszawie
            : new DateTimeOffset(2026, 9, 25, 19, 30, 0, TimeSpan.Zero); // 21:30 w Warszawie
        var attendance = new AttendanceOrigin(new GeoPoint(49.798, 19.08), mode);
        await using var host = await RoutingTestHost.StartAsync(RoutingTestHost.CreateEvent(end), attendance);

        using var response = await host.Client.GetAsync(ValidUrl);

        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("returnGap").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("returns").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetRoute_WhenDeclaredModeIsUnknown_ReturnsBadRequestProblemWithoutPlanning()
    {
        var planner = new FixedPlanner();
        var unknownMode = new AttendanceOrigin(new GeoPoint(49.798, 19.08), TransportMode.Unknown);
        await using var host = await RoutingTestHost.StartAsync(RoutingTestHost.CreateEvent(), unknownMode, planner);

        using var response = await host.Client.GetAsync(ValidUrl);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
        planner.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRoute_UsesInjectedPlanner()
    {
        var planner = new FixedPlanner();
        await using var host = await RoutingTestHost.StartAsync(
            RoutingTestHost.CreateEvent(), PublicTransportFromHome, planner);

        using var response = await host.Client.GetAsync(ValidUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        planner.Requests.Should().ContainSingle().Which.Mode.Should().Be(TransportMode.PublicTransport);
    }

    [Fact]
    public async Task GetRoute_WhenEventMissing_ReturnsNotFoundProblem()
    {
        await using var host = await RoutingTestHost.StartAsync(null, PublicTransportFromHome);

        using var response = await host.Client.GetAsync(ValidUrl);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRoute_WhenUserDidNotDeclareAttendance_ReturnsNotFoundProblem()
    {
        await using var host = await RoutingTestHost.StartAsync(RoutingTestHost.CreateEvent(), null);

        using var response = await host.Client.GetAsync(ValidUrl);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task GetRoute_WithMissingOrInvalidUserId_ReturnsBadRequestProblem(string? userId)
    {
        await using var host = await RoutingTestHost.StartAsync(RoutingTestHost.CreateEvent(), PublicTransportFromHome);

        using var response = await host.Client.GetAsync(RouteUrl(RoutingTestHost.EventId.ToString(), userId));

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task GetRoute_WithInvalidEventId_ReturnsBadRequestProblem(string eventId)
    {
        await using var host = await RoutingTestHost.StartAsync(RoutingTestHost.CreateEvent(), PublicTransportFromHome);

        using var response = await host.Client.GetAsync(RouteUrl(eventId, RoutingTestHost.AttendingUserId.ToString()));

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }

    private sealed class FixedPlanner : IRoutePlanner
    {
        public List<RouteRequest> Requests { get; } = [];

        public Task<RoutePlan> PlanAsync(RouteRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var start = request.EventStartAt.AddMinutes(-30);
            var step = new RouteStep(RouteStepType.Walk, "Idz.", 10);
            var outbound = new JourneyOption(10, start, start.AddMinutes(10), [step]);
            return Task.FromResult(new RoutePlan(PlannerSource.Demo, outbound, [], false));
        }
    }
}
