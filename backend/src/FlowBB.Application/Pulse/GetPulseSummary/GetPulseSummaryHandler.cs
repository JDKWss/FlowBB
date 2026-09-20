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
        var snapshots = await reader.GetEventsWithPointsAsync(cancellationToken);
        var allPoints = snapshots.SelectMany(snapshot => snapshot.Points).ToList();

        var participantsWithoutReturn = snapshots.Sum(snapshot =>
            DemoReturnGapPolicy.CountParticipantsWithoutReturn(
                snapshot.Event.EndAt,
                ModalSplit.From(snapshot.Points)));

        return new PulseSummary(
            clock.GetUtcNow(),
            snapshots.Count,
            allPoints.Count,
            ModalSplit.From(allPoints),
            participantsWithoutReturn);
    }
}
