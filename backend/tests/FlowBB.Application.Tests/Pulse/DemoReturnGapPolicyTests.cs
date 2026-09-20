using FlowBB.Application.Pulse;
using FlowBB.Domain.Common;
using FluentAssertions;

namespace FlowBB.Application.Tests.Pulse;

public class DemoReturnGapPolicyTests
{
    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    [Theory]
    // Czas letni (CEST, UTC+2): 22:00 lokalnie to 20:00 UTC.
    [InlineData(2026, 9, 25, 19, 59, false)] // 21:59
    [InlineData(2026, 9, 25, 20, 0, true)]   // 22:00 (granica wlacznie)
    [InlineData(2026, 9, 25, 21, 15, true)]  // 23:15 (Nocny Bieg z seedu)
    // Czas zimowy (CET, UTC+1): 22:00 lokalnie to 21:00 UTC.
    [InlineData(2026, 1, 15, 20, 59, false)] // 21:59
    [InlineData(2026, 1, 15, 21, 0, true)]   // 22:00
    // Dni zmiany czasu: po przejsciu 29.03 obowiazuje UTC+2, po przejsciu 25.10 obowiazuje UTC+1.
    [InlineData(2026, 3, 29, 19, 59, false)] // 21:59 CEST
    [InlineData(2026, 3, 29, 20, 0, true)]   // 22:00 CEST
    [InlineData(2026, 10, 25, 20, 59, false)] // 21:59 CET
    [InlineData(2026, 10, 25, 21, 0, true)]  // 22:00 CET
    public void IsLateEvent_UsesLocalWarsawTime(int year, int month, int day, int hour, int minute, bool expected)
    {
        DemoReturnGapPolicy.IsLateEvent(Utc(year, month, day, hour, minute)).Should().Be(expected);
    }

    [Fact]
    public void IsLateEvent_WithoutEndAt_IsFalse()
    {
        DemoReturnGapPolicy.IsLateEvent(null).Should().BeFalse();
    }

    [Fact]
    public void IsLateEvent_ConvertsOffsetsToWarsaw()
    {
        // Ta sama chwila (22:30 w Warszawie) zapisana z innym offsetem daje ten sam wynik.
        var inWarsawOffset = new DateTimeOffset(2026, 9, 25, 22, 30, 0, TimeSpan.FromHours(2));
        var inNewYorkOffset = new DateTimeOffset(2026, 9, 25, 16, 30, 0, TimeSpan.FromHours(-4));

        DemoReturnGapPolicy.IsLateEvent(inWarsawOffset).Should().BeTrue();
        DemoReturnGapPolicy.IsLateEvent(inNewYorkOffset).Should().BeTrue();
    }

    [Fact]
    public void CountParticipantsWithoutReturn_CountsOnlyPublicTransportForLateEvent()
    {
        var split = new ModalSplit(PublicTransport: 21, Walking: 18, Bike: 6, Car: 7, Unknown: 3);

        DemoReturnGapPolicy.CountParticipantsWithoutReturn(Utc(2026, 9, 25, 21, 0), split).Should().Be(21);
        DemoReturnGapPolicy.CountParticipantsWithoutReturn(Utc(2026, 9, 25, 19, 0), split).Should().Be(0);
        DemoReturnGapPolicy.CountParticipantsWithoutReturn(null, split).Should().Be(0);
    }

    [Theory]
    [InlineData(TransportMode.PublicTransport, true)]
    [InlineData(TransportMode.Walking, false)]
    [InlineData(TransportMode.Bike, false)]
    [InlineData(TransportMode.Car, false)]
    [InlineData(TransportMode.Unknown, false)]
    public void HasReturnGap_ForLateEvent_OnlyForPublicTransport(TransportMode mode, bool expected)
    {
        DemoReturnGapPolicy.HasReturnGap(Utc(2026, 9, 25, 21, 0), mode).Should().Be(expected);
    }

    [Fact]
    public void HasReturnGap_ForEarlyEventOrMissingEnd_IsFalse()
    {
        DemoReturnGapPolicy.HasReturnGap(Utc(2026, 9, 25, 19, 0), TransportMode.PublicTransport).Should().BeFalse();
        DemoReturnGapPolicy.HasReturnGap(null, TransportMode.PublicTransport).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateAlert_WithoutParticipants_ReturnsNull(int participants)
    {
        DemoReturnGapPolicy.CreateAlert(participants).Should().BeNull();
    }

    [Theory]
    [InlineData(1, "1 osoba nie ma dogodnego powrotu po 22:00.")]
    [InlineData(2, "2 osoby nie maja dogodnego powrotu po 22:00.")]
    [InlineData(4, "4 osoby nie maja dogodnego powrotu po 22:00.")]
    [InlineData(5, "5 osob nie ma dogodnego powrotu po 22:00.")]
    [InlineData(12, "12 osob nie ma dogodnego powrotu po 22:00.")]
    [InlineData(14, "14 osob nie ma dogodnego powrotu po 22:00.")]
    [InlineData(21, "21 osob nie ma dogodnego powrotu po 22:00.")]
    [InlineData(22, "22 osoby nie maja dogodnego powrotu po 22:00.")]
    public void CreateAlert_ReturnsWarningWithCountAndHour(int participants, string message)
    {
        var alert = DemoReturnGapPolicy.CreateAlert(participants);

        alert.Should().Be(new PulseAlert(PulseAlertCode.ReturnGap, PulseAlertSeverity.Warning, message));
    }
}
