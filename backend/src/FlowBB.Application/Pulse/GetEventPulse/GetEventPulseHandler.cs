using FlowBB.Application.Abstractions.Persistence;

namespace FlowBB.Application.Pulse.GetEventPulse;

/// <summary>KPI wydarzenia. Luka powrotowa i alert pochodza z <see cref="DemoReturnGapPolicy"/>.</summary>
public sealed record EventPulse(
    Guid EventId,
    string EventName,
    DateTimeOffset GeneratedAt,
    int ParticipantsCount,
    ModalSplit ModalSplit,
    int ParticipantsWithoutReturn,
    IReadOnlyList<PulseAlert> Alerts);

public sealed class GetEventPulseHandler(IPulseDataReader reader, TimeProvider clock)
{
    /// <returns>KPI wydarzenia lub <c>null</c>, gdy wydarzenie nie istnieje.</returns>
    public async Task<EventPulse?> HandleAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event id cannot be an empty GUID.", nameof(eventId));
        }

        var info = await reader.GetEventAsync(eventId, cancellationToken);
        if (info is null)
        {
            return null;
        }

        var points = await reader.GetPointsAsync(eventId, cancellationToken);
        var modalSplit = ModalSplit.From(points);
        var withoutReturn = DemoReturnGapPolicy.CountParticipantsWithoutReturn(info.EndAt, modalSplit);
        var alert = DemoReturnGapPolicy.CreateAlert(withoutReturn);
        return new EventPulse(
            info.Id,
            info.Name,
            clock.GetUtcNow(),
            points.Count,
            modalSplit,
            withoutReturn,
            alert is null ? [] : [alert]);
    }
}
