using FlowBB.Infrastructure.Routing.Mzk;
using FluentAssertions;

namespace FlowBB.Infrastructure.Tests.Routing.Mzk;

public sealed class MzkServiceCalendarTests
{
    private static MzkServiceCalendar Calendar() => SyntheticTimetable.Build([]).Calendar;

    [Theory]
    [InlineData("2026-09-19", "Saturday")]
    [InlineData("2026-09-20", "Sunday")]
    [InlineData("2026-09-22", "Weekday")]
    [InlineData("2026-11-01", "Sunday")]
    public void TryGetService_ReturnsTheServicePrintedInTheCalendar(string day, string expected)
    {
        Calendar().TryGetService(DateOnly.Parse(day), out var service).Should().BeTrue();

        service.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-08-31")]
    [InlineData("2027-01-05")]
    public void TryGetService_OutsideTheCalendar_IsRefusedInsteadOfGuessedFromTheWeekday(string day)
    {
        Calendar().TryGetService(DateOnly.Parse(day), out _).Should().BeFalse();
    }

    [Fact]
    public void Calendar_ReportsItsRange()
    {
        var calendar = Calendar();

        calendar.FirstDay.Should().Be(new DateOnly(2026, 9, 19));
        calendar.LastDay.Should().Be(new DateOnly(2026, 12, 26));
    }

    [Theory]
    [InlineData("2026-12-25", "FlagHoliday")]
    [InlineData("2027-01-01", "FlagHoliday")]
    [InlineData("2026-04-05", "FlagHoliday")]
    [InlineData("2027-03-28", "FlagHoliday")]
    [InlineData("2026-11-01", "Ordinary")]
    [InlineData("2026-11-11", "Ordinary")]
    [InlineData("2026-12-26", "Ordinary")]
    [InlineData("2026-09-22", "Ordinary")]
    public void DayKindOf_IsFlagHolidayOnlyForTheDaysNamedInTheLegends(string day, string expected)
    {
        // 1 i 11 listopada oraz 26 grudnia sa swietami, ale legendy N, R i W ich nie dotycza. Uzycie pola
        // public_holiday skasowaloby kursy wieczorne tych dni i pokazalo nieistniejaca luke powrotowa.
        MzkServiceCalendar.DayKindOf(DateOnly.Parse(day)).ToString().Should().Be(expected);
    }

    [Fact]
    public void WindowsFrom_AtMidday_IsASingleWindowForToday()
    {
        var windows = Calendar().WindowsFrom(new DateOnly(2026, 9, 22), SyntheticTimetable.Sec(12, 0), 6 * 3600);

        windows.Should().ContainSingle().Which.Day.Should().Be(new DateOnly(2026, 9, 22));
    }

    [Fact]
    public void WindowsFrom_LateEvening_AlsoCoversTheNightTailOfTheNextDay()
    {
        var windows = Calendar().WindowsFrom(new DateOnly(2026, 9, 22), SyntheticTimetable.Sec(23, 25), 6 * 3600);

        windows.Should().HaveCount(2);
        windows[1].Day.Should().Be(new DateOnly(2026, 9, 23));
        windows[1].FromSecond.Should().Be(0);
        windows[1].ToSecond.Should().Be(MzkServiceCalendar.NightTailEndSecond);
    }

    [Fact]
    public void WindowsFrom_WhenTheNextDayIsOutsideTheCalendar_ReturnsOnlyTodayAsWindow()
    {
        var windows = Calendar().WindowsFrom(new DateOnly(2026, 12, 26), SyntheticTimetable.Sec(23, 25), 6 * 3600);

        windows.Should().ContainSingle().Which.Day.Should().Be(new DateOnly(2026, 12, 26));
    }

    [Fact]
    public void ToInstant_OnTheDstFallbackNight_UsesTheStandardOffset()
    {
        // 2026-10-25 02:50 zdarza sie dwa razy; wybieramy offset standardowy (ZALOZENIE Z-4).
        var instant = MzkLocalTime.ToInstant(new DateOnly(2026, 10, 25), SyntheticTimetable.Sec(2, 50));

        instant.Offset.Should().Be(TimeSpan.FromHours(1));
    }
}
