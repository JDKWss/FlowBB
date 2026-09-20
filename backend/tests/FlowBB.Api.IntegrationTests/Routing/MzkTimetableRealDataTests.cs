using System.Text.Json;
using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;
using FlowBB.Infrastructure.Routing;
using FlowBB.Infrastructure.Routing.Mzk;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowBB.Api.IntegrationTests.Routing;

/// <summary>
/// Planer na prawdziwych plikach data/gtfs/mzk/parsed/ osadzonych w assembly, przez publiczny planer z fallbackiem.
/// Godziny oczekiwane pochodza z rozkladu MZK (linia 7, sobota) i z osobnej, niezaleznej symulacji na tych danych.
/// </summary>
public sealed class MzkTimetableRealDataTests
{
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly GeoPoint DemoUserHome = new(49.81272, 19.03384);
    private static readonly GeoPoint ConcertVenue = new(49.82245, 19.04431);
    private static readonly GeoPoint RunVenue = new(49.79381, 19.04955);

    private static TimetableFallbackRoutePlanner Planner(ILogger<TimetableFallbackRoutePlanner>? logger = null) => new(
        new MzkTimetableRoutePlanner(new MzkTimetableProvider(NullLogger<MzkTimetableProvider>.Instance)),
        new DemoRoutePlanner(),
        logger ?? NullLogger<TimetableFallbackRoutePlanner>.Instance);

    // Koncert na Rynku z seedu demo: sobota 2026-09-19, 19:00-21:30.
    private static RouteRequest Concert() => new(
        EventId,
        DateTimeOffset.Parse("2026-09-19T19:00:00+02:00"),
        DateTimeOffset.Parse("2026-09-19T21:30:00+02:00"),
        ConcertVenue,
        DemoUserHome,
        TransportMode.PublicTransport);

    [Fact]
    public async Task Concert_IsPlannedFromThePrintedTimetableOfLineSeven()
    {
        var plan = await Planner().PlanAsync(Concert());

        plan.Source.Should().Be(PlannerSource.MzkTimetable);
        var transit = plan.Outbound.Steps.Single(step => step.Type == RouteStepType.Transit);
        transit.Line.Should().Be("7");
        transit.Instruction.Should().Contain("18:28 Karpacka Osiedle Karpackie").And.Contain("18:35 Hotel Prezydent");
        transit.DurationMinutes.Should().Be(7);
        plan.Outbound.Stops!.Select(stop => stop.Name).Should().Equal("Karpacka Osiedle Karpackie", "Hotel Prezydent");
    }

    [Fact]
    public async Task Concert_ArrivesBeforeTheEventStarts()
    {
        var plan = await Planner().PlanAsync(Concert());

        plan.Outbound.ArrivalAt.Should().BeOnOrBefore(DateTimeOffset.Parse("2026-09-19T18:50:00+02:00"));
        plan.Outbound.DurationMinutes.Should().Be(plan.Outbound.Steps.Sum(step => step.DurationMinutes));
    }

    [Fact]
    public async Task Concert_HasARealReturnWithinTheThreshold_SoThereIsNoReturnGap()
    {
        var plan = await Planner().PlanAsync(Concert());

        // Gotowosc 21:40; najwczesniejsza linia 7 z przystanku przy Rynku to 22:13 (33 minuty, ponizej progu 45).
        // Planer przeszukuje wszystkie przystanki w promieniu 800 m po obu stronach, wiec znajduje wczesniejszy
        // powrot niz proste odwrocenie pary z dojazdu (22:27).
        plan.ReturnGap.Should().BeFalse();
        plan.Returns.Should().HaveCount(2);
        var first = plan.Returns[0].Steps.Single(step => step.Type == RouteStepType.Transit);
        first.Line.Should().Be("7");
        first.Instruction.Should().Contain("22:13 Plac Żwirki i Wigury");
    }

    [Fact]
    public async Task LateEvening_WhenTheLastSaturdayBusHasGone_HasReturnGapAndNoReturns()
    {
        var lateConcert = new RouteRequest(
            EventId,
            DateTimeOffset.Parse("2026-09-19T20:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-19T22:30:00+02:00"),
            ConcertVenue,
            DemoUserHome,
            TransportMode.PublicTransport);

        var plan = await Planner().PlanAsync(lateConcert);

        plan.Source.Should().Be(PlannerSource.MzkTimetable);
        plan.ReturnGap.Should().BeTrue();
        plan.Returns.Should().BeEmpty();
    }

    [Fact]
    public async Task Concert_TheResponseNeverContainsTheUsersHomeCoordinates()
    {
        var plan = await Planner().PlanAsync(Concert());

        var json = JsonSerializer.Serialize(plan);

        json.Should().NotContain("49.81272").And.NotContain("19.03384");
    }

    [Fact]
    public async Task Concert_IsTheSamePlanEveryTime()
    {
        var planner = Planner();

        var first = JsonSerializer.Serialize(await planner.PlanAsync(Concert()));
        var second = JsonSerializer.Serialize(await planner.PlanAsync(Concert()));

        second.Should().Be(first);
    }

    [Fact]
    public async Task NightRun_IsServedOnlyByTheNightLine_SoItFallsBackToTheDemoPlanner()
    {
        var run = new RouteRequest(
            EventId,
            DateTimeOffset.Parse("2026-09-20T21:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-20T23:15:00+02:00"),
            RunVenue,
            DemoUserHome,
            TransportMode.PublicTransport);

        var plan = await Planner().PlanAsync(run);

        plan.Source.Should().Be(PlannerSource.Demo);
        plan.Outbound.Stops.Should().BeNull();
    }

    [Fact]
    public async Task Degradation_IsLoggedWithoutAnyCoordinates()
    {
        var logger = new ListLogger<TimetableFallbackRoutePlanner>();
        var run = new RouteRequest(
            EventId,
            DateTimeOffset.Parse("2026-09-20T21:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-20T23:15:00+02:00"),
            RunVenue,
            DemoUserHome,
            TransportMode.PublicTransport);

        await Planner(logger).PlanAsync(run);

        logger.Messages.Should().ContainSingle();
        logger.Messages[0].Should().NotContain("49.8").And.NotContain("19.0");
    }

    [Fact]
    public async Task DateAfterTheCalendarEnds_FallsBackToTheDemoPlanner()
    {
        var request = new RouteRequest(
            EventId,
            DateTimeOffset.Parse("2027-02-10T19:00:00+01:00"),
            DateTimeOffset.Parse("2027-02-10T21:00:00+01:00"),
            ConcertVenue,
            DemoUserHome,
            TransportMode.PublicTransport);

        (await Planner().PlanAsync(request)).Source.Should().Be(PlannerSource.Demo);
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        internal List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
