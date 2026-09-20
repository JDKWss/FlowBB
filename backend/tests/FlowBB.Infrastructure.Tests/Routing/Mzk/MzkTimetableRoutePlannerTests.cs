using System.Text.Json;
using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;
using FlowBB.Infrastructure.Routing.Mzk;
using FluentAssertions;
using static FlowBB.Infrastructure.Tests.Routing.Mzk.SyntheticTimetable;

namespace FlowBB.Infrastructure.Tests.Routing.Mzk;

public sealed class MzkTimetableRoutePlannerTests
{
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly TimeSpan Warsaw = TimeSpan.FromHours(2);

    private static MzkTimetableRoutePlanner Planner(MzkTimetable timetable) => new(new MzkTimetableProvider(timetable));

    private static RouteRequest Request(string start, string end, GeoPoint? origin = null, TransportMode mode = TransportMode.PublicTransport) =>
        new(EventId, DateTimeOffset.Parse(start), DateTimeOffset.Parse(end), StopD, origin ?? StopA, mode);

    // Wtorek 2026-09-22, wydarzenie 12:00-14:00; dojazd ma sie zakonczyc do 11:50.
    private static RouteRequest Tuesday(GeoPoint? origin = null) => Request("2026-09-22T12:00:00+02:00", "2026-09-22T14:00:00+02:00", origin);

    private static RoutePlan Plan(MzkTimetable timetable, RouteRequest request)
    {
        var outcome = Planner(timetable).Plan(request);
        outcome.Failure.Should().BeNull(outcome.Detail);
        return outcome.Plan!;
    }

    [Fact]
    public void Plan_ChoosesTheLatestDepartureThatStillArrivesInTime()
    {
        var plan = Plan(Standard(), Tuesday());

        plan.Source.Should().Be(PlannerSource.MzkTimetable);
        var transit = plan.Outbound.Steps.Single(step => step.Type == RouteStepType.Transit);
        transit.Line.Should().Be("T1");
        // Odjazd z A 11:30 dojezdza do D o 11:39; kolejny 12:00 byl by za pozno.
        transit.Instruction.Should().Contain("11:30 A").And.Contain("11:39 D");
        transit.DurationMinutes.Should().Be(9);
    }

    [Fact]
    public void Plan_BuildsWalkWaitTransitWalkSteps_WithTheWalkMarkedAsAnEstimate()
    {
        var plan = Plan(Standard(), Tuesday());

        plan.Outbound.Steps.Select(step => step.Type).Should().Equal(
            RouteStepType.Walk, RouteStepType.Wait, RouteStepType.Transit, RouteStepType.Walk);
        plan.Outbound.Steps[0].Instruction.Should().Contain("przystanku A").And.Contain("szacunek");
        plan.Outbound.Steps[3].Instruction.Should().Contain("na miejsce wydarzenia").And.Contain("szacunek");
        plan.Outbound.Steps[2].Instruction.Should().Contain("rozklad MZK");
    }

    [Fact]
    public void Plan_DurationAndTimesAreConsistentWithTheSteps()
    {
        var journey = Plan(Standard(), Tuesday()).Outbound;

        journey.DurationMinutes.Should().Be(journey.Steps.Sum(step => step.DurationMinutes));
        (journey.ArrivalAt - journey.DepartureAt).TotalMinutes.Should().Be(journey.DurationMinutes);
        journey.ArrivalAt.Should().BeOnOrBefore(DateTimeOffset.Parse("2026-09-22T11:50:00+02:00"));
    }

    [Fact]
    public void Plan_ReturnsTheBoardingAndAlightingStops_WithoutTheUsersOrigin()
    {
        var plan = Plan(Standard(), Tuesday());

        var stops = plan.Outbound.Stops!;
        stops.Select(stop => stop.Name).Should().Equal("A", "D");
        stops[0].Location.Should().Be(StopA);
        stops[1].Location.Should().Be(StopD);
    }

    [Fact]
    public void Plan_DoesNotExposeTheOriginWhenItIsNotAStopLocation()
    {
        var origin = new GeoPoint(49.8104, 19.0306);

        var json = JsonSerializer.Serialize(Plan(Standard(), Tuesday(origin)));

        json.Should().NotContain("49.8104").And.NotContain("19.0306");
    }

    [Fact]
    public void Plan_SkipsRunsThatDoNotReachTheAlightingStop()
    {
        // Kurs z A o 11:30 (na C bylby o 11:36) nie ma pary na stronie C: kurs skrocony nie dowozi do D, wybor pada na 11:00.
        var timetable = Build([.. Forward(skipAtC: 1130), .. Back(5, 35)]);

        var transit = Plan(timetable, Tuesday()).Outbound.Steps.Single(step => step.Type == RouteStepType.Transit);

        transit.Instruction.Should().Contain("11:00 A").And.Contain("11:09 D");
    }

    [Fact]
    public void Plan_ReturnUsesTheReverseDirectionOfTheSameLine()
    {
        var plan = Plan(Standard(5, 35), Tuesday());

        var transit = plan.Returns[0].Steps.Single(step => step.Type == RouteStepType.Transit);
        transit.Line.Should().Be("T1");
        plan.Returns[0].Stops!.Select(stop => stop.Name).Should().Equal("D", "A");
    }

    [Fact]
    public void Plan_OffersTheTwoEarliestReturnsAfterTheEvent()
    {
        var plan = Plan(Standard(5, 35), Tuesday());

        // Koniec 14:00 + 10 min = 14:10; minimalne dojscie 1 min; pierwszy odjazd z D o 14:35, drugi 15:05.
        plan.Returns.Should().HaveCount(2);
        plan.Returns[0].Steps.Single(step => step.Type == RouteStepType.Transit).Instruction.Should().Contain("14:35 D");
        plan.Returns[1].Steps.Single(step => step.Type == RouteStepType.Transit).Instruction.Should().Contain("15:05 D");
    }

    [Fact]
    public void Plan_WhenTheWaitAfterTheEventIsShort_HasNoReturnGap()
    {
        Plan(Standard(5, 35), Tuesday()).ReturnGap.Should().BeFalse();
    }

    [Fact]
    public void Plan_WhenTheWaitExceedsTheThreshold_SetsReturnGapButKeepsTheLateOptions()
    {
        // Odjazdy z D co godzine o :56. Gotowosc 14:10, pierwszy odjazd 14:56 to 46 minut, czyli ponad progiem 45.
        var plan = Plan(Standard(56), Tuesday());

        plan.ReturnGap.Should().BeTrue();
        plan.Returns.Should().NotBeEmpty("prawdziwa, choc pozna opcja jest cenniejsza niz pusta lista");
    }

    [Fact]
    public void Plan_WhenTheWaitEqualsTheThreshold_HasNoReturnGap()
    {
        // Odjazd o :55 to dokladnie 45 minut po gotowosci 14:10; prog jest "wiecej niz 45".
        Plan(Standard(55), Tuesday()).ReturnGap.Should().BeFalse();
    }

    [Fact]
    public void Plan_WhenNoDepartureIsLeftAfterTheEvent_HasReturnGapAndNoReturns()
    {
        // Wydarzenie do 23:30; ostatni powrot z D o 22:35, a linia nocna nie istnieje w tym rozkladzie.
        var request = Request("2026-09-22T21:00:00+02:00", "2026-09-22T23:30:00+02:00");

        var plan = Plan(Standard(5, 35), request);

        plan.ReturnGap.Should().BeTrue();
        plan.Returns.Should().BeEmpty();
    }

    [Fact]
    public void Plan_WhenOnlyTheNightLineIsLeft_ReportsTheGapButOffersTheNightOption()
    {
        var timetable = Build([.. Forward(), .. Back(5, 35), .. Night()]);
        var request = Request("2026-09-22T21:00:00+02:00", "2026-09-22T23:30:00+02:00");

        var plan = Plan(timetable, request);

        // Gotowosc 23:40, kurs nocny N1 o 00:30 nastepnej doby to 50 minut czekania.
        plan.ReturnGap.Should().BeTrue();
        var first = plan.Returns.First().Steps.Single(step => step.Type == RouteStepType.Transit);
        first.Line.Should().Be("N1");
        first.Instruction.Should().Contain("00:30 D");
    }

    [Fact]
    public void Plan_WithoutEventEnd_AssumesATwoHourEvent()
    {
        var request = new RouteRequest(EventId, DateTimeOffset.Parse("2026-09-22T12:00:00+02:00"), null, StopD, StopA, TransportMode.PublicTransport);

        var plan = Plan(Standard(5, 35), request);

        // 12:00 + 2 h + 10 min = 14:10.
        plan.Returns[0].Steps.Single(step => step.Type == RouteStepType.Transit).Instruction.Should().Contain("14:35 D");
    }

    [Fact]
    public void Plan_WhenTheOriginHasNoStopNearby_FailsWithoutRevealingCoordinates()
    {
        var farAway = new GeoPoint(49.7000, 18.9000);

        var outcome = Planner(Standard()).Plan(Tuesday(farAway));

        outcome.Plan.Should().BeNull();
        outcome.Failure.Should().Be(MzkPlanningFailure.NoStopNearOrigin);
        outcome.Detail.Should().NotContain("49.7").And.NotContain("18.9");
    }

    [Fact]
    public void Plan_WhenTheEventHasNoStopNearby_Fails()
    {
        var request = new RouteRequest(
            EventId, DateTimeOffset.Parse("2026-09-22T12:00:00+02:00"), null, new GeoPoint(49.7000, 18.9000), StopA, TransportMode.PublicTransport);

        Planner(Standard()).Plan(request).Failure.Should().Be(MzkPlanningFailure.NoStopNearDestination);
    }

    [Fact]
    public void Plan_WhenTheDateIsOutsideTheCalendar_IsRefused()
    {
        var request = Request("2027-01-05T12:00:00+01:00", "2027-01-05T14:00:00+01:00");

        var outcome = Planner(Standard()).Plan(request);

        outcome.Failure.Should().Be(MzkPlanningFailure.DateOutsideCalendar);
        outcome.Detail.Should().Contain("2027-01-05");
    }

    [Fact]
    public void Plan_WhenNoDirectConnectionArrivesInTime_Fails()
    {
        // Wydarzenie o 08:05: odjazd 08:00 z A dociera do D dopiero o 08:09, po terminie.
        var request = Request("2026-09-22T08:05:00+02:00", "2026-09-22T10:00:00+02:00");

        Planner(Standard()).Plan(request).Failure.Should().Be(MzkPlanningFailure.NoOutboundConnection);
    }

    [Fact]
    public void Plan_WhenTheTimetableIsUnusable_ReportsItAsUnavailable()
    {
        var timetable = Build([.. Forward()], withStops: false);

        Planner(timetable).Plan(Tuesday()).Failure.Should().Be(MzkPlanningFailure.TimetableUnavailable);
    }

    [Fact]
    public void Plan_ForAnotherTransportMode_IsRefused()
    {
        Planner(Standard()).Plan(Request("2026-09-22T12:00:00+02:00", "2026-09-22T14:00:00+02:00", mode: TransportMode.Walking))
            .Failure.Should().Be(MzkPlanningFailure.UnsupportedMode);
    }

    [Fact]
    public void Plan_IsDeterministic()
    {
        var planner = Planner(Standard(5, 35));

        var first = JsonSerializer.Serialize(planner.Plan(Tuesday()).Plan);
        var second = JsonSerializer.Serialize(planner.Plan(Tuesday()).Plan);

        second.Should().Be(first);
    }

    [Fact]
    public void Plan_KeepsEveryInstructionWithinTheContractLimit_ForVeryLongStopNames()
    {
        var longName = new string('X', 110);
        var pages = Forward().Concat(Back(5, 35)).Select(page => page with { Stop = longName + page.Stop }).ToArray();
        var timetable = MzkTimetableLoaderFor(pages, longName);

        var plan = Plan(timetable, Tuesday());

        plan.Outbound.Steps.Should().OnlyContain(step => step.Instruction.Length <= RouteStep.InstructionMaxLength);
        plan.Returns.SelectMany(journey => journey.Steps).Should().OnlyContain(step => step.Instruction.Length <= RouteStep.InstructionMaxLength);
    }

    private static MzkTimetable MzkTimetableLoaderFor(Page[] pages, string prefix)
    {
        var stops = "[" + string.Join(",", new[] { ("A", StopA), ("B", StopB), ("C", StopC), ("D", StopD) }.Select(stop =>
            $"{{\"stop_name\":\"{prefix}{stop.Item1}\",\"latitude\":{stop.Item2.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"longitude\":{stop.Item2.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}")) + "]";
        return MzkTimetableLoader.Load(
            new MemoryStream(System.Text.Encoding.UTF8.GetBytes(DeparturesJson(pages))),
            new MemoryStream(System.Text.Encoding.UTF8.GetBytes(CalendarJson(CalendarDays))),
            new MemoryStream(System.Text.Encoding.UTF8.GetBytes(stops)),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
    }
}
