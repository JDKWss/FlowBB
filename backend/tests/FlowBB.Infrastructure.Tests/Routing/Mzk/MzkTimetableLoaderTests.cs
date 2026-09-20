using System.Text;
using FlowBB.Infrastructure.Routing.Mzk;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static FlowBB.Infrastructure.Tests.Routing.Mzk.SyntheticTimetable;

namespace FlowBB.Infrastructure.Tests.Routing.Mzk;

public sealed class MzkTimetableLoaderTests
{
    private static MzkTimetable Single(params Dep[] deps) =>
        Build([new Page("T1", "Forward", 1, "A", deps), new Page("T1", "Forward", 2, "B", deps)]);

    private static int Count(MzkTimetable timetable, MzkDayKind kind) =>
        timetable.Patterns.Single().Stops[0].Board.Count(MzkServiceType.Weekday, kind);

    [Fact]
    public void Load_SkipsDepotRuns()
    {
        var timetable = Single(new Dep(Sec(9, 0)), new Dep(Sec(9, 30), ["#"], DepotRun: true));

        Count(timetable, MzkDayKind.Ordinary).Should().Be(1);
        Count(timetable, MzkDayKind.FlagHoliday).Should().Be(1);
    }

    [Fact]
    public void Load_ForAnOrdinaryDay_ExcludesDeparturesThatRunOnlyOnLegendDays()
    {
        var timetable = Single(new Dep(Sec(9, 0)), new Dep(Sec(9, 30), ["W"]));

        Count(timetable, MzkDayKind.Ordinary).Should().Be(1);
        Count(timetable, MzkDayKind.FlagHoliday).Should().Be(2);
    }

    [Theory]
    [InlineData("N")]
    [InlineData("R")]
    public void Load_ForALegendDay_ExcludesDeparturesThatDoNotRunThen(string flag)
    {
        var timetable = Single(new Dep(Sec(9, 0)), new Dep(Sec(9, 30), [flag]));

        Count(timetable, MzkDayKind.Ordinary).Should().Be(2);
        Count(timetable, MzkDayKind.FlagHoliday).Should().Be(1);
    }

    [Fact]
    public void Load_ForShortenedRuns_KeepsTheDepartureBecauseTheChainDecidesWhetherItArrives()
    {
        var timetable = Single(new Dep(Sec(9, 0)), new Dep(Sec(9, 30), ["K"]), new Dep(Sec(10, 0), ["D"]));

        Count(timetable, MzkDayKind.Ordinary).Should().Be(3);
    }

    [Fact]
    public void Load_SkipsADepartureWithAnUnknownFlag()
    {
        var timetable = Single(new Dep(Sec(9, 0)), new Dep(Sec(9, 30), ["X"]));

        Count(timetable, MzkDayKind.Ordinary).Should().Be(1);
    }

    [Fact]
    public void Load_ReadsPrintedTravelTimeBetweenNeighbouringStops()
    {
        var pattern = Standard().Patterns.Single(p => p.Direction == "Forward");

        pattern.HopMinutes(MzkServiceType.Weekday, 0).Should().Be(3);
        pattern.HopMinutes(MzkServiceType.Weekday, 2).Should().Be(3);
    }

    [Fact]
    public void Load_WhenTheSecondPageHasFarFewerDepartures_MarksTheSegmentUnknown()
    {
        // Odpowiednik kursow skroconych: wiekszosc odjazdow z pierwszej strony nie ma pary na drugiej, wiec
        // najblizszy odjazd jest z nastepnego kursu i moda czasu jazdy nie przekracza progu.
        var timetable = Build([
            new Page("T1", "Forward", 1, "A", HalfHourly(0)),
            new Page("T1", "Forward", 2, "B", At((9, 3), (13, 3), (17, 3), (21, 3)))
        ]);

        timetable.Patterns.Single().HopMinutes(MzkServiceType.Weekday, 0).Should().Be(-1);
    }

    [Fact]
    public void Load_WithoutStopCoordinates_IsNotUsable()
    {
        Build([.. Forward(), .. Back(5)], withStops: false).IsUsable.Should().BeFalse();
    }

    [Fact]
    public void Load_WithAllResources_IsUsable()
    {
        Standard().IsUsable.Should().BeTrue();
    }

    [Fact]
    public void Load_RejectsStopsOutsideBielsko_SoAWrongSchemaCannotSilentlyProduceStopsAtZeroZero()
    {
        var departures = new MemoryStream(Encoding.UTF8.GetBytes(DeparturesJson(Forward())));
        var calendar = new MemoryStream(Encoding.UTF8.GetBytes(CalendarJson(CalendarDays)));
        var stops = new MemoryStream(Encoding.UTF8.GetBytes(
            "[{\"stop_name\":\"A\",\"latitude\":0,\"longitude\":0},{\"stop_name\":\"B\",\"latitude\":49.8150,\"longitude\":19.0400}]"));

        var timetable = MzkTimetableLoader.Load(departures, calendar, stops, NullLogger.Instance);

        timetable.Stops.Count.Should().Be(1);
    }

    [Fact]
    public void Load_SkipsCalendarDaysWithAnUnknownService()
    {
        var timetable = Build([], calendar: ["2026-09-22|weekday", "2026-09-23|holiday"]);

        timetable.Calendar.TryGetService(new DateOnly(2026, 9, 22), out _).Should().BeTrue();
        timetable.Calendar.TryGetService(new DateOnly(2026, 9, 23), out _).Should().BeFalse();
    }

    [Fact]
    public void LoadOrEmpty_ProductionResources_AreEmbeddedAndUsable()
    {
        var timetable = MzkTimetableLoader.LoadOrEmpty(NullLogger.Instance);

        timetable.IsUsable.Should().BeTrue();
        timetable.Patterns.Should().HaveCount(10);
        timetable.Stops.Count.Should().Be(85);
    }
}
