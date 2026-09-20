using FlowBB.Application.Events;
using FluentAssertions;

namespace FlowBB.Application.Tests.Events;

public class EventWithParticipantsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(83)]
    public void Constructor_WithNonNegativeCount_CreatesInstance(int count)
    {
        var result = new EventWithParticipants(EventTestData.CreateEvent(), count);

        result.ParticipantsCount.Should().Be(count);
    }

    [Fact]
    public void Constructor_WithNegativeCount_Throws()
    {
        var act = () => new EventWithParticipants(EventTestData.CreateEvent(), -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNullEvent_Throws()
    {
        var act = () => new EventWithParticipants(null!, 1);

        act.Should().Throw<ArgumentNullException>();
    }
}
