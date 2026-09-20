using FlowBB.Application.Abstractions.Persistence;

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
        var participantsWithoutReturn = 0;
        foreach (var info in events)
        {
            var points = await reader.GetPointsAsync(info.Id, cancellationToken);
            allPoints.AddRange(points);
            participantsWithoutReturn += DemoReturnGapPolicy.CountParticipantsWithoutReturn(info.EndAt, ModalSplit.From(points));
        }

        return new PulseSummary(
            clock.GetUtcNow(),
            events.Count,
            allPoints.Count,
            ModalSplit.From(allPoints),
            participantsWithoutReturn);
    }
}
