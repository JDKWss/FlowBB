using System.Text.Json;
using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;
using FlowBB.Infrastructure.Routing;
using FluentAssertions;

namespace FlowBB.Api.IntegrationTests.Routing;

// DemoRoutePlanner nie ma zaleznosci od sieci ani bazy, wiec to testy jednostkowe. Leza w tym projekcie, bo tylko on
// referencuje Infrastructure (Api -> Infrastructure), a nowy csproj wymagalby osobnej zgody.
public class DemoRoutePlannerTests
{
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly TimeSpan Warsaw = TimeSpan.FromHours(2);
    private static readonly DateTimeOffset Start = new(2026, 9, 25, 19, 0, 0, Warsaw);
    private static readonly GeoPoint Rynek = new(49.82245, 19.04431);
    private static readonly GeoPoint Home = new(49.79800, 19.08000);

    private readonly DemoRoutePlanner _planner = new();

    private static RouteRequest Request(
        TransportMode mode, DateTimeOffset? endAt = null, GeoPoint? origin = null) =>
        new(EventId, Start, endAt, Rynek, origin ?? Home, mode);

    private static DateTimeOffset At(int hour, int minute) => new(2026, 9, 25, hour, minute, 0, Warsaw);

    private static int ExpectedMinutes(double speedKmh) =>
        Math.Max(1, (int)Math.Ceiling(GeoDistance.KilometersBetween(Home, Rynek) / speedKmh * 60));

    [Theory]
    [InlineData(TransportMode.Walking)]
    [InlineData(TransportMode.PublicTransport)]
    [InlineData(TransportMode.Bike)]
    [InlineData(TransportMode.Car)]
    public async Task PlanAsync_SameInput_GivesIdenticalResultOver100Calls(TransportMode mode)
    {
        var request = Request(mode, At(21, 30));
        var expected = JsonSerializer.Serialize(await _planner.PlanAsync(request));

        for (var i = 0; i < 100; i++)
        {
            JsonSerializer.Serialize(await _planner.PlanAsync(request)).Should().Be(expected);
        }
    }

    [Theory]
    [InlineData(TransportMode.Walking, new[] { RouteStepType.Walk })]
    [InlineData(TransportMode.Bike, new[] { RouteStepType.Bike })]
    [InlineData(TransportMode.Car, new[] { RouteStepType.Car })]
    [InlineData(TransportMode.PublicTransport,
        new[] { RouteStepType.Walk, RouteStepType.Wait, RouteStepType.Transit, RouteStepType.Walk })]
    public async Task PlanAsync_BuildsStepsMatchingTransportMode(TransportMode mode, RouteStepType[] expectedTypes)
    {
        var plan = await _planner.PlanAsync(Request(mode, At(21, 30)));

        plan.Source.Should().Be(PlannerSource.Demo);
        plan.Outbound.Steps.Select(step => step.Type).Should().Equal(expectedTypes);
        plan.Returns[0].Steps.Select(step => step.Type).Should().Equal(expectedTypes);
    }

    [Fact]
    public void Request_WithUnknownMode_IsRejectedInsteadOfPlannedAsPublicTransport()
    {
        var act = () => Request(TransportMode.Unknown);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("mode");
    }

    [Theory]
    [InlineData(TransportMode.Walking, 4.8)]
    [InlineData(TransportMode.Bike, 15)]
    [InlineData(TransportMode.Car, 30)]
    public async Task PlanAsync_UsesApprovedSpeedForSingleLegModes(TransportMode mode, double speedKmh)
    {
        var plan = await _planner.PlanAsync(Request(mode));

        plan.Outbound.DurationMinutes.Should().Be(ExpectedMinutes(speedKmh));
        plan.Returns.Single().DurationMinutes.Should().Be(ExpectedMinutes(speedKmh));
    }

    [Fact]
    public async Task PlanAsync_PublicTransportIsStaticDemoConnectionAtBusSpeed()
    {
        var plan = await _planner.PlanAsync(Request(TransportMode.PublicTransport, At(21, 30)));

        var bus = ExpectedMinutes(22);
        plan.Outbound.Steps.Select(step => step.DurationMinutes).Should().Equal(6, 5, bus, 4);
        plan.Returns[0].Steps.Select(step => step.DurationMinutes).Should().Equal(4, 5, bus, 6);
        plan.Outbound.Steps.Single(step => step.Type == RouteStepType.Transit).Line.Should().Be("7 (demo)");
    }

    [Fact]
    public async Task PlanAsync_OutboundArrivesTenMinutesBeforeStartAndDurationMatchesSteps()
    {
        var plan = await _planner.PlanAsync(Request(TransportMode.Car));

        plan.Outbound.ArrivalAt.Should().Be(Start.AddMinutes(-10));
        plan.Outbound.DurationMinutes.Should().Be(plan.Outbound.Steps.Sum(step => step.DurationMinutes));
        plan.Outbound.DepartureAt.Should().Be(plan.Outbound.ArrivalAt.AddMinutes(-plan.Outbound.DurationMinutes));
    }

    [Fact]
    public async Task PlanAsync_FasterModesGiveShorterTripsOverTheSameDistance()
    {
        var walking = await _planner.PlanAsync(Request(TransportMode.Walking));
        var car = await _planner.PlanAsync(Request(TransportMode.Car));

        car.Outbound.DurationMinutes.Should().BeLessThan(walking.Outbound.DurationMinutes);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(150)]
    [InlineData(210)]
    [InlineData(290)]
    [InlineData(480)]
    public async Task PlanAsync_PublicTransport_AlwaysOffersTwoStaticReturnsRegardlessOfTimeOfDay(int minutesAfterStart)
    {
        var end = Start.AddMinutes(minutesAfterStart);

        var plan = await _planner.PlanAsync(Request(TransportMode.PublicTransport, end));

        plan.Returns.Select(option => option.DepartureAt)
            .Should().Equal(end.AddMinutes(10), end.AddMinutes(40));
        plan.ReturnGap.Should().BeFalse();
    }

    [Theory]
    [InlineData(TransportMode.Walking)]
    [InlineData(TransportMode.Bike)]
    [InlineData(TransportMode.Car)]
    public async Task PlanAsync_IndividualModes_OfferSingleReturnEvenLateAtNight(TransportMode mode)
    {
        var plan = await _planner.PlanAsync(Request(mode, At(23, 30)));

        plan.Returns.Select(option => option.DepartureAt).Should().Equal(At(23, 40));
        plan.ReturnGap.Should().BeFalse();
    }

    [Fact]
    public async Task PlanAsync_WithoutEndAt_AssumesTwoHourEvent()
    {
        var plan = await _planner.PlanAsync(Request(TransportMode.Car));

        plan.Returns.Single().DepartureAt.Should().Be(Start.AddHours(2).AddMinutes(10));
    }

    [Fact]
    public async Task PlanAsync_WhenOriginEqualsDestination_StillReturnsValidJourney()
    {
        var plan = await _planner.PlanAsync(Request(TransportMode.Walking, At(21, 30), origin: Rynek));

        plan.Outbound.Steps.Should().OnlyContain(step => step.DurationMinutes >= 1);
    }

    [Fact]
    public async Task PlanAsync_WithCancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _planner.PlanAsync(Request(TransportMode.Car), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Planner_PreservesParameterlessConstruction()
    {
        typeof(DemoRoutePlanner).GetConstructors()
            .Should().Contain(constructor => constructor.GetParameters().Length == 0);
    }
}
