using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Abstractions.Routing;
using FlowBB.Application.Routing;
using FlowBB.Application.Routing.GetEventRoute;
using FlowBB.Domain.Common;
using FlowBB.Domain.Events;
using FlowBB.Domain.Routing;
using FluentAssertions;
using Moq;

namespace FlowBB.Application.Tests.Routing;

public class GetEventRouteHandlerTests
{
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset Start = new(2026, 9, 25, 19, 0, 0, TimeSpan.FromHours(2));
    private static readonly GeoPoint Venue = new(49.82245, 19.04431);
    private static readonly GeoPoint Home = new(49.798, 19.08);

    private readonly Mock<IEventLookup> _events = new();
    private readonly Mock<IAttendanceOriginLookup> _origins = new();
    private readonly Mock<IRoutePlanner> _planner = new();

    private GetEventRouteHandler CreateHandler() => new(_events.Object, _origins.Object, _planner.Object);

    private static Event CreateEvent(DateTimeOffset? endAt) => new(
        EventId, "Koncert", "Opis", Start, endAt, "Rynek", EventCategory.Culture, EventSource.Demo, Venue);

    private static RoutePlan CreatePlan()
    {
        var step = new RouteStep(RouteStepType.Walk, "Idz.", 5);
        return new RoutePlan(
            PlannerSource.Demo, new JourneyOption(5, Start, Start.AddMinutes(5), [step]), [], false);
    }

    private void SetupEvent(Event? found) =>
        _events.Setup(e => e.FindByIdAsync(EventId, It.IsAny<CancellationToken>())).ReturnsAsync(found);

    private void SetupOrigin(AttendanceOrigin? origin) =>
        _origins.Setup(o => o.FindAsync(EventId, UserId, It.IsAny<CancellationToken>())).ReturnsAsync(origin);

    [Fact]
    public async Task HandleAsync_BuildsRequestFromEventAndAttendanceAndReturnsPlan()
    {
        var end = Start.AddHours(2);
        var plan = CreatePlan();
        RouteRequest? captured = null;
        SetupEvent(CreateEvent(end));
        SetupOrigin(new AttendanceOrigin(Home, TransportMode.Bike));
        _planner.Setup(p => p.PlanAsync(It.IsAny<RouteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RouteRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(plan);

        var result = await CreateHandler().HandleAsync(EventId, UserId);

        result.Status.Should().Be(GetEventRouteStatus.Ok);
        result.Plan.Should().BeSameAs(plan);
        captured.Should().NotBeNull();
        captured!.EventId.Should().Be(EventId);
        captured.EventStartAt.Should().Be(Start);
        captured.EventEndAt.Should().Be(end);
        captured.Destination.Should().Be(Venue);
        captured.Origin.Should().Be(Home);
        captured.Mode.Should().Be(TransportMode.Bike);
    }

    [Fact]
    public async Task HandleAsync_WhenEventHasNoEnd_PassesNullEnd()
    {
        RouteRequest? captured = null;
        SetupEvent(CreateEvent(null));
        SetupOrigin(new AttendanceOrigin(Home, TransportMode.Car));
        _planner.Setup(p => p.PlanAsync(It.IsAny<RouteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RouteRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(CreatePlan());

        await CreateHandler().HandleAsync(EventId, UserId);

        captured!.EventEndAt.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenEventMissing_ReturnsEventNotFoundWithoutTouchingOtherPorts()
    {
        SetupEvent(null);

        var result = await CreateHandler().HandleAsync(EventId, UserId);

        result.Status.Should().Be(GetEventRouteStatus.EventNotFound);
        result.Plan.Should().BeNull();
        _origins.VerifyNoOtherCalls();
        _planner.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenUserDidNotDeclareAttendance_ReturnsAttendanceNotFoundWithoutPlanning()
    {
        SetupEvent(CreateEvent(null));
        SetupOrigin(null);

        var result = await CreateHandler().HandleAsync(EventId, UserId);

        result.Status.Should().Be(GetEventRouteStatus.AttendanceNotFound);
        result.Plan.Should().BeNull();
        _planner.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenModeIsUnknown_ReturnsInvalidTransportModeWithoutPlanning()
    {
        SetupEvent(CreateEvent(null));
        SetupOrigin(new AttendanceOrigin(Home, TransportMode.Unknown));

        var result = await CreateHandler().HandleAsync(EventId, UserId);

        result.Status.Should().Be(GetEventRouteStatus.InvalidTransportMode);
        result.Plan.Should().BeNull();
        _planner.VerifyNoOtherCalls();
    }

    private static RoutePlan PlanFrom(PlannerSource source, bool returnGap, bool withReturn)
    {
        var step = new RouteStep(RouteStepType.Walk, "Idz.", 5);
        var journey = new JourneyOption(5, Start, Start.AddMinutes(5), [step]);
        return new RoutePlan(source, journey, withReturn ? [journey] : [], returnGap);
    }

    private async Task<RoutePlan> PlanFor(RoutePlan planned, DateTimeOffset end)
    {
        SetupEvent(CreateEvent(end));
        SetupOrigin(new AttendanceOrigin(Home, TransportMode.PublicTransport));
        _planner.Setup(p => p.PlanAsync(It.IsAny<RouteRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(planned);

        return (await CreateHandler().HandleAsync(EventId, UserId)).Plan!;
    }

    [Fact]
    public async Task HandleAsync_ForTheDemoPlannerAndALateEvent_KeepsApplyingTheDemoReturnGapRule()
    {
        var late = new DateTimeOffset(2026, 9, 25, 23, 15, 0, TimeSpan.FromHours(2));

        var plan = await PlanFor(PlanFrom(PlannerSource.Demo, returnGap: false, withReturn: true), late);

        plan.ReturnGap.Should().BeTrue();
        plan.Returns.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ForTheTimetablePlannerAndALateEvent_KeepsTheLateReturnOptionAndItsOwnGap()
    {
        var late = new DateTimeOffset(2026, 9, 25, 23, 15, 0, TimeSpan.FromHours(2));
        var planned = PlanFrom(PlannerSource.MzkTimetable, returnGap: true, withReturn: true);

        var plan = await PlanFor(planned, late);

        // Regula 22:00 nie moze nadpisac wyniku planera, ktory zna rozklad: pozna opcja powrotu ma zostac.
        plan.Should().BeSameAs(planned);
        plan.Returns.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsync_ForTheTimetablePlannerAndALateEvent_DoesNotInventAGapTheTimetableDoesNotHave()
    {
        var late = new DateTimeOffset(2026, 9, 25, 23, 15, 0, TimeSpan.FromHours(2));

        var plan = await PlanFor(PlanFrom(PlannerSource.MzkTimetable, returnGap: false, withReturn: true), late);

        plan.ReturnGap.Should().BeFalse();
        plan.Returns.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_WithEmptyIds_ThrowsAndCallsNothing(bool emptyEvent)
    {
        var act = () => CreateHandler().HandleAsync(emptyEvent ? Guid.Empty : EventId, emptyEvent ? UserId : Guid.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
        _events.VerifyNoOtherCalls();
    }
}
