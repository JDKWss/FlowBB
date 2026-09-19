using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Pulse.GetEventPulse;

namespace FlowBB.Application.Pulse.GetPulseSummary;

public sealed record PulseSummary(
    DateTimeOffset GeneratedAt,
    int EventsCount,
    int ParticipantsCount,
    ModalSplit ModalSplit,
    int ParticipantsWithoutReturn);

public sealed class GetPulseSummaryHandler(IPulseDataReader reader, TimeProvider clock)
{
    public async Task<PulseSummary> HandleAsync(CancellationToken cancellationToken = default)
    {
        var events = await reader.GetEventsAsync(cancellationToken);

        var allPoints = new List<PulsePoint>();
        foreach (var info in events)
        {
            allPoints.AddRange(await reader.GetPointsAsync(info.Id, cancellationToken));
        }

        return new PulseSummary(
            clock.GetUtcNow(),
            events.Count,
            allPoints.Count,
            ModalSplit.From(allPoints),
            GetEventPulseHandler.ParticipantsWithoutReturnInMvp);
    }
}
