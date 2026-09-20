using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Pulse;
using FlowBB.Application.Pulse.GetActivityMap;
using FlowBB.Application.Pulse.GetEventPulse;
using FlowBB.Application.Pulse.GetPulseHexagons;
using FlowBB.Application.Pulse.GetPulseSummary;
using FlowBB.Domain.Common;
using FluentAssertions;
using Moq;

namespace FlowBB.Application.Tests.Pulse;

public class PulseHandlersTests
{
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherEventId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static List<PulsePoint> Points(int count, TransportMode mode) =>
        Enumerable.Range(0, count).Select(_ => new PulsePoint(49.8225, 19.0444, mode)).ToList();

    private static Mock<IPulseDataReader> Reader(params (Guid Id, string Name, List<PulsePoint> Points)[] events)
    {
        var reader = new Mock<IPulseDataReader>();
        reader.Setup(r => r.GetEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(events.Select(e => new PulseEventInfo(e.Id, e.Name)).ToList());
        reader.Setup(r => r.GetEventsWithPointsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(events.Select(e =>
                new PulseEventSnapshot(new PulseEventInfo(e.Id, e.Name), e.Points)).ToList());
        foreach (var (id, name, points) in events)
        {
            reader.Setup(r => r.GetEventAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new PulseEventInfo(id, name));
            reader.Setup(r => r.GetPointsAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(points);
        }

        return reader;
    }

    [Fact]
    public async Task EventPulse_ReturnsNameCountAndModalSplit()
    {
        var points = Points(3, TransportMode.Walking).Concat(Points(2, TransportMode.Car)).ToList();
        var handler = new GetEventPulseHandler(Reader((EventId, "Koncert", points)).Object, new FixedTimeProvider(Now));

        var pulse = await handler.HandleAsync(EventId);

        pulse.Should().NotBeNull();
        pulse.EventId.Should().Be(EventId);
        pulse.EventName.Should().Be("Koncert");
        pulse.GeneratedAt.Should().Be(Now);
        pulse.ParticipantsCount.Should().Be(5);
        pulse.ModalSplit.Should().Be(new ModalSplit(0, 3, 0, 2, 0));
        pulse.ParticipantsWithoutReturn.Should().Be(0);
    }

    // 21:00 UTC = 23:00 w Warszawie (CEST) -> pozny koniec; 19:30 UTC = 21:30 -> wczesny koniec.
    private static readonly DateTimeOffset LateEnd = new(2026, 9, 25, 21, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EarlyEnd = new(2026, 9, 25, 19, 30, 0, TimeSpan.Zero);

    private static Mock<IPulseDataReader> ReaderWithEnd(
        Guid id, string name, List<PulsePoint> points, DateTimeOffset? endAt)
    {
        var reader = new Mock<IPulseDataReader>();
        var info = new PulseEventInfo(id, name, endAt);
        reader.Setup(r => r.GetEventsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([info]);
        reader.Setup(r => r.GetEventsWithPointsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PulseEventSnapshot(info, points)]);
        reader.Setup(r => r.GetEventAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(info);
        reader.Setup(r => r.GetPointsAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(points);
        return reader;
    }

    [Fact]
    public async Task EventPulse_ForLateEvent_CountsPublicTransportAndAddsReturnGapAlert()
    {
        var points = Points(3, TransportMode.PublicTransport).Concat(Points(4, TransportMode.Walking)).ToList();
        var handler = new GetEventPulseHandler(
            ReaderWithEnd(EventId, "Nocny Bieg", points, LateEnd).Object, new FixedTimeProvider(Now));

        var pulse = await handler.HandleAsync(EventId);

        pulse!.ParticipantsWithoutReturn.Should().Be(3);
        var alert = pulse.Alerts.Should().ContainSingle().Subject;
        alert.Code.Should().Be(PulseAlertCode.ReturnGap);
        alert.Severity.Should().Be(PulseAlertSeverity.Warning);
        alert.Message.Should().Be("3 osoby nie maja dogodnego powrotu po 22:00.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EventPulse_ForEarlyEventOrMissingEnd_HasNoGapAndNoAlert(bool hasEnd)
    {
        var points = Points(5, TransportMode.PublicTransport);
        var handler = new GetEventPulseHandler(
            ReaderWithEnd(EventId, "Koncert", points, hasEnd ? EarlyEnd : null).Object, new FixedTimeProvider(Now));

        var pulse = await handler.HandleAsync(EventId);

        pulse!.ParticipantsWithoutReturn.Should().Be(0);
        pulse.Alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task Summary_SumsParticipantsWithoutReturnOfLateEventsOnly()
    {
        var reader = new Mock<IPulseDataReader>();
        var late = new PulseEventInfo(EventId, "Pozne", LateEnd);
        var early = new PulseEventInfo(OtherEventId, "Wczesne", EarlyEnd);
        reader.Setup(r => r.GetEventsWithPointsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new(late, Points(4, TransportMode.PublicTransport).Concat(Points(3, TransportMode.Bike)).ToList()),
            new(early, Points(6, TransportMode.PublicTransport))
        ]);
        var handler = new GetPulseSummaryHandler(reader.Object, new FixedTimeProvider(Now));

        var summary = await handler.HandleAsync();

        summary.ParticipantsWithoutReturn.Should().Be(4);
    }

    [Fact]
    public async Task EventPulse_ForUnknownEvent_ReturnsNull()
    {
        var handler = new GetEventPulseHandler(Reader().Object, new FixedTimeProvider(Now));

        (await handler.HandleAsync(EventId)).Should().BeNull();
    }

    [Fact]
    public async Task EventPulse_ForEmptyId_Throws()
    {
        var handler = new GetEventPulseHandler(Reader().Object, new FixedTimeProvider(Now));

        var act = () => handler.HandleAsync(Guid.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Summary_SumsAllEvents()
    {
        var reader = Reader(
            (EventId, "A", Points(4, TransportMode.Bike)),
            (OtherEventId, "B", Points(2, TransportMode.PublicTransport)));
        var handler = new GetPulseSummaryHandler(reader.Object, new FixedTimeProvider(Now));

        var summary = await handler.HandleAsync();

        summary.EventsCount.Should().Be(2);
        summary.ParticipantsCount.Should().Be(6);
        summary.ModalSplit.Should().Be(new ModalSplit(2, 0, 4, 0, 0));
        summary.GeneratedAt.Should().Be(Now);
        reader.Verify(r => r.GetEventsWithPointsAsync(It.IsAny<CancellationToken>()), Times.Once);
        reader.Verify(r => r.GetEventsAsync(It.IsAny<CancellationToken>()), Times.Never);
        reader.Verify(r => r.GetPointsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Summary_WithNoEvents_IsZero()
    {
        var handler = new GetPulseSummaryHandler(Reader().Object, new FixedTimeProvider(Now));

        var summary = await handler.HandleAsync();

        summary.EventsCount.Should().Be(0);
        summary.ParticipantsCount.Should().Be(0);
    }

    [Theory]
    [InlineData(9, 0)]
    [InlineData(10, 1)]
    [InlineData(11, 1)]
    public async Task Hexagons_FollowPrivacyThreshold(int participants, int expectedCells)
    {
        var reader = Reader((EventId, "A", Points(participants, TransportMode.Walking))).Object;
        var handler = new GetPulseHexagonsHandler(reader, new GetActivityMapHandler(reader));

        var cells = await handler.HandleAsync(EventId);

        cells.Should().HaveCount(expectedCells);
    }

    [Fact]
    public async Task Hexagons_ForUnknownEvent_ReturnsNull()
    {
        var reader = Reader().Object;
        var handler = new GetPulseHexagonsHandler(reader, new GetActivityMapHandler(reader));

        (await handler.HandleAsync(EventId)).Should().BeNull();
    }

    [Fact]
    public async Task Hexagons_ForEmptyId_Throws()
    {
        var reader = Reader().Object;
        var handler = new GetPulseHexagonsHandler(reader, new GetActivityMapHandler(reader));

        var act = () => handler.HandleAsync(Guid.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
