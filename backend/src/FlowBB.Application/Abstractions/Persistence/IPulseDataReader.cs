using FlowBB.Application.Pulse;

namespace FlowBB.Application.Abstractions.Persistence;

/// <summary>
/// Wewnetrzny odczyt danych do agregacji PULSE. Zwraca wylacznie punkty startu i tryb transportu
/// deklaracji wydarzenia, bez identyfikatorow uzytkownikow. Implementuje go adapter Neo4j.
/// </summary>
public interface IPulseDataReader
{
    Task<IReadOnlyList<PulsePoint>> GetPointsAsync(Guid eventId, CancellationToken cancellationToken = default);
}
