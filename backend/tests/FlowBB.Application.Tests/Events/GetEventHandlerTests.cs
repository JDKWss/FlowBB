using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Events;
using FlowBB.Application.Events.GetEvent;
using FlowBB.Domain.Common;
using FluentAssertions;
using Moq;

namespace FlowBB.Application.Tests.Events;

public class GetEventHandlerTests
{
    private static (GetEventHandler Handler, Mock<IEventRepository> Repository) Create(EventWithParticipants? found)
    {
        var repository = new Mock<IEventRepository>();
        repository
            .Setup(r => r.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(found);
        return (new GetEventHandler(repository.Object), repository);
    }

    [Fact]
    public async Task HandleAsync_WhenEventExists_ReturnsDetailsWithParticipantCount()
    {
        var found = EventTestData.CreateEventWithParticipants(participants: 82);
        var (handler, _) = Create(found);

        var result = await handler.HandleAsync(EventTestData.EventId);

        result.Should().NotBeNull();
        result!.Event.Should().Be(found.Event);
        result.ParticipantsCount.Should().Be(82);
    }

    [Fact]
    public async Task HandleAsync_WhenEventExists_UsesMvpDefaultsForCrewAndTransportModes()
    {
        var (handler, _) = Create(EventTestData.CreateEventWithParticipants());

        var result = await handler.HandleAsync(EventTestData.EventId);

        result!.CrewAvailable.Should().BeFalse();
        result.AvailableTransportModes.Should().Equal(
            TransportMode.Walking, TransportMode.PublicTransport, TransportMode.Bike, TransportMode.Car);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_WithAndWithoutEndAt_ReturnsEvent(bool hasEndAt)
    {
        DateTimeOffset? endAt = hasEndAt ? EventTestData.Start.AddHours(2) : null;
        var (handler, _) = Create(EventTestData.CreateEventWithParticipants(endAt: endAt));

        var result = await handler.HandleAsync(EventTestData.EventId);

        result!.Event.EndAt.Should().Be(endAt);
    }

    [Fact]
    public async Task HandleAsync_WhenEventDoesNotExist_ReturnsNull()
    {
        var (handler, _) = Create(null);

        var result = await handler.HandleAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WithEmptyId_ThrowsAndDoesNotCallRepository()
    {
        var (handler, repository) = Create(null);

        var act = () => handler.HandleAsync(Guid.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
        repository.Verify(r => r.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PassesIdAndCancellationTokenToRepository()
    {
        var (handler, repository) = Create(null);
        using var cts = new CancellationTokenSource();

        await handler.HandleAsync(EventTestData.EventId, cts.Token);

        repository.Verify(r => r.FindAsync(EventTestData.EventId, cts.Token), Times.Once);
    }
}
