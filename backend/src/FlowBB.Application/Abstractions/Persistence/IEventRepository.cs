using FlowBB.Application.Events;

namespace FlowBB.Application.Abstractions.Persistence;

/// <summary>
/// Port odczytu wydarzen. Implementuje go adapter Neo4j. Liczba uczestnikow jest wyliczana z relacji
/// <c>IS_GOING_TO</c>, a nie przechowywana.
/// </summary>
public interface IEventRepository
{
    /// <summary>
    /// Zwraca wydarzenia posortowane rosnaco po <c>StartAt</c>. Granice <paramref name="from"/> i
    /// <paramref name="to"/> sa opcjonalne, wlacznie, i dotycza <c>StartAt</c>.
    /// </summary>
    Task<IReadOnlyList<EventWithParticipants>> ListAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);

    Task<EventWithParticipants?> FindAsync(Guid eventId, CancellationToken cancellationToken = default);
}
