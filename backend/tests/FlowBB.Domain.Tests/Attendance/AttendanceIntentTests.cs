using FluentAssertions;
using FlowBB.Domain.Attendance;
using FlowBB.Domain.Common;

namespace FlowBB.Domain.Tests.Attendance;

public sealed class AttendanceIntentTests
{
    [Fact]
    public void Constructor_WithValidValues_CreatesAttendanceAndNormalizesTimeToUtc()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var updatedAt = new DateTimeOffset(2026, 9, 19, 19, 20, 0, TimeSpan.FromHours(2));

        var attendance = new AttendanceIntent(
            eventId,
            userId,
            TransportMode.PublicTransport,
            updatedAt);

        attendance.EventId.Should().Be(eventId);
        attendance.UserId.Should().Be(userId);
        attendance.TransportMode.Should().Be(TransportMode.PublicTransport);
        attendance.UpdatedAt.Should().Be(new DateTimeOffset(2026, 9, 19, 17, 20, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Constructor_WithEmptyEventId_ThrowsArgumentException()
    {
        var action = () => new AttendanceIntent(
            Guid.Empty,
            Guid.NewGuid(),
            TransportMode.Walking,
            DateTimeOffset.UtcNow);

        action.Should()
            .Throw<ArgumentException>()
            .WithParameterName("eventId");
    }

    [Fact]
    public void Constructor_WithEmptyUserId_ThrowsArgumentException()
    {
        var action = () => new AttendanceIntent(
            Guid.NewGuid(),
            Guid.Empty,
            TransportMode.Walking,
            DateTimeOffset.UtcNow);

        action.Should()
            .Throw<ArgumentException>()
            .WithParameterName("userId");
    }

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    public void Constructor_WithUndefinedTransportMode_ThrowsArgumentOutOfRangeException(int value)
    {
        var transportMode = (TransportMode)value;

        var action = () => new AttendanceIntent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            transportMode,
            DateTimeOffset.UtcNow);

        action.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithParameterName("transportMode");
    }
}
