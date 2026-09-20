using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Events;
using FlowBB.Application.Events.GetEvents;
using FluentAssertions;
using Moq;

namespace FlowBB.Application.Tests.Events;

public class GetEventsHandlerTests
{
    private static readonly DateTimeOffset From = EventTestData.Start;
    private static readonly DateTimeOffset To = EventTestData.Start.AddDays(7);

    private static (GetEventsHandler Handler, Mock<IEventRepository> Repository) Create(
        IReadOnlyList<EventWithParticipants> items)
    {
        var repository = new Mock<IEventRepository>();
        repository
            .Setup(r => r.ListAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
        return (new GetEventsHandler(repository.Object), repository);
    }

    [Fact]
    public async Task HandleAsync_WithNoEvents_ReturnsEmptyList()
    {
        var (handler, _) = Create([]);

        var result = await handler.HandleAsync(new GetEventsQuery(null, null));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WithEvents_ReturnsThemWithParticipantCounts()
    {
        var first = EventTestData.CreateEventWithParticipants(participants: 82);
        var second = EventTestData.CreateEventWithParticipants(Guid.NewGuid(), participants: 0, endAt: EventTestData.Start.AddHours(2));
        var (handler, _) = Create([first, second]);

        var result = await handler.HandleAsync(new GetEventsQuery(null, null));

        result.Should().Equal(first, second);
        result[0].ParticipantsCount.Should().Be(82);
        result[1].Event.EndAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_PassesRangeAndCancellationTokenToRepository()
    {
        var (handler, repository) = Create([]);
        using var cts = new CancellationTokenSource();

        await handler.HandleAsync(new GetEventsQuery(From, To), cts.Token);

        repository.Verify(r => r.ListAsync(From, To, cts.Token), Times.Once);
    }

    [Theory]
    [MemberData(nameof(ValidRanges))]
    public async Task HandleAsync_WithValidRange_CallsRepository(DateTimeOffset? from, DateTimeOffset? to)
    {
        var (handler, repository) = Create([]);

        await handler.HandleAsync(new GetEventsQuery(from, to));

        repository.Verify(r => r.ListAsync(from, to, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithFromLaterThanTo_ThrowsAndDoesNotCallRepository()
    {
        var (handler, repository) = Create([]);

        var act = () => handler.HandleAsync(new GetEventsQuery(To, From));

        await act.Should().ThrowAsync<ArgumentException>();
        repository.Verify(
            r => r.ListAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithNullQuery_Throws()
    {
        var (handler, _) = Create([]);

        var act = () => handler.HandleAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    public static TheoryData<DateTimeOffset?, DateTimeOffset?> ValidRanges() => new()
    {
        { null, null },
        { From, null },
        { null, To },
        { From, To },
        { From, From }
    };
}
