using FlowBB.Application.Events;
using FlowBB.Domain.Common;
using FlowBB.Domain.Events;

namespace FlowBB.Application.Tests.Events;

internal static class EventTestData
{
    public static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly DateTimeOffset Start = new(2026, 9, 25, 18, 0, 0, TimeSpan.FromHours(2));

    public static Event CreateEvent(Guid? id = null, DateTimeOffset? endAt = null) =>
        new(
            id ?? EventId,
            "Koncert na Rynku",
            "Wieczorny koncert.",
            Start,
            endAt,
            "Rynek",
            EventCategory.Culture,
            EventSource.Demo,
            new GeoPoint(49.82245, 19.04431));

    public static EventWithParticipants CreateEventWithParticipants(
        Guid? id = null,
        int participants = 82,
        DateTimeOffset? endAt = null) =>
        new(CreateEvent(id, endAt), participants);
}
