using FlowBB.Application.Routing;

namespace FlowBB.Application.Abstractions.Persistence;

/// <summary>
/// Port odczytu snapshotu deklaracji "Ide" (relacja <c>IS_GOING_TO</c>) dla pary uzytkownik-wydarzenie.
/// Implementuje go adapter Neo4j. Zwraca <c>null</c>, gdy uzytkownik nie zadeklarowal udzialu.
/// </summary>
public interface IAttendanceOriginLookup
{
    Task<AttendanceOrigin?> FindAsync(Guid eventId, Guid userId, CancellationToken cancellationToken = default);
}
