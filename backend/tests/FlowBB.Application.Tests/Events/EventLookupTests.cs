using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Events;
using FluentAssertions;
using Moq;

namespace FlowBB.Application.Tests.Events;

public class EventLookupTests
{
    private static EventLookup Create(EventWithParticipants? found)
    {
        var repository = new Mock<IEventRepository>();
        repository
            .Setup(r => r.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(found);
        return new EventLookup(repository.Object);
    }

    [Fact]
    public async Task ExistsAsync_WhenEventExists_ReturnsTrue()
    {
        var lookup = Create(EventTestData.CreateEventWithParticipants());

        (await lookup.ExistsAsync(EventTestData.EventId)).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenEventDoesNotExist_ReturnsFalse()
    {
        var lookup = Create(null);

        (await lookup.ExistsAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task FindByIdAsync_WhenEventExists_ReturnsEventWithoutParticipantCount()
    {
        var found = EventTestData.CreateEventWithParticipants();
        var lookup = Create(found);

        var result = await lookup.FindByIdAsync(EventTestData.EventId);

        result.Should().Be(found.Event);
    }

    [Fact]
    public async Task FindByIdAsync_WhenEventDoesNotExist_ReturnsNull()
    {
        var lookup = Create(null);

        (await lookup.FindByIdAsync(Guid.NewGuid())).Should().BeNull();
    }
}
