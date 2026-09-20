using FlowBB.Domain.Common;

namespace FlowBB.Domain.Attendance;

public sealed record AttendanceIntent
{
    public AttendanceIntent(
        Guid eventId,
        Guid userId,
        TransportMode transportMode,
        DateTimeOffset updatedAt)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event id cannot be empty.", nameof(eventId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        }

        if (!Enum.IsDefined(transportMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(transportMode),
                transportMode,
                "Transport mode is not supported.");
        }

        EventId = eventId;
        UserId = userId;
        TransportMode = transportMode;
        UpdatedAt = updatedAt.ToUniversalTime();
    }

    public Guid EventId { get; }

    public Guid UserId { get; }

    public TransportMode TransportMode { get; }

    public DateTimeOffset UpdatedAt { get; }
}
