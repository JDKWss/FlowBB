using FlowBB.Application.Crews;

namespace FlowBB.Application.Abstractions.Persistence;

/// <summary>
/// Port persystencji Crew. Implementuje go adapter Neo4j. <c>Crew.Join</c> w domenie nie jest bezpieczne watkowo,
/// wiec <see cref="TryJoinAsync"/> musi wykonac sprawdzenie i zapis atomowo (jedna transakcja).
/// </summary>
public interface ICrewRepository
{
    /// <summary>Grupy wydarzenia. <paramref name="userId"/> (opcjonalny) ustala <c>JoinedByCurrentUser</c>.</summary>
    Task<IReadOnlyList<CrewSummary>> ListByEventAsync(
        Guid eventId, Guid? userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomowo: brak grupy lub uzytkownika, juz czlonek, czlonek innej grupy tego samego wydarzenia (jedna grupa na
    /// wydarzenie; do grup roznych wydarzen mozna nalezec rownoczesnie), pelna grupa albo dopisanie. Ponowienie dla
    /// czlonka nie zmienia licznika ani <paramref name="joinedAt"/> (wlasciwosc relacji <c>MEMBER_OF</c>).
    /// </summary>
    Task<CrewJoinResult> TryJoinAsync(
        Guid crewId, Guid userId, DateTimeOffset joinedAt, CancellationToken cancellationToken = default);

    /// <summary>Idempotentnie usuwa czlonkostwo. Brak grupy lub czlonkostwa nie jest bledem.</summary>
    Task LeaveAsync(Guid crewId, Guid userId, CancellationToken cancellationToken = default);
}
