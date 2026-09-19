using FlowBB.Application.Abstractions.Persistence;

namespace FlowBB.Application.Pulse.GetEventPulse;

/// <summary>KPI wydarzenia. Alerty nie sa jeszcze liczone: w MVP brak logiki powrotow.</summary>
public sealed record EventPulse(
    Guid EventId,
    string EventName,
    DateTimeOffset GeneratedAt,
    int ParticipantsCount,
    ModalSplit ModalSplit,
    int ParticipantsWithoutReturn);

public sealed class GetEventPulseHandler(IPulseDataReader reader, TimeProvider clock)
{
    /// <summary>Jawne zalozenie MVP: logika powrotow nie istnieje, wiec zawsze 0.</summary>
    public const int ParticipantsWithoutReturnInMvp = 0;

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
        return new EventPulse(
            info.Id,
            info.Name,
            clock.GetUtcNow(),
            points.Count,
            ModalSplit.From(points),
            ParticipantsWithoutReturnInMvp);
    }
}
