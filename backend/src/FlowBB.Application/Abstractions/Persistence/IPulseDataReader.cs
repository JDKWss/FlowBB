using FlowBB.Application.Pulse;

namespace FlowBB.Application.Abstractions.Persistence;

/// <summary>
/// Wewnetrzny odczyt danych do agregacji PULSE. Zwraca wylacznie punkty startu i tryb transportu
/// deklaracji wydarzenia, bez identyfikatorow uzytkownikow. Implementuje go adapter Neo4j.
/// </summary>
public interface IPulseDataReader
{
    /// <summary>Punkty startu deklaracji wydarzenia. Pusta lista, gdy nikt sie nie zadeklarowal.</summary>
    Task<IReadOnlyList<PulsePoint>> GetPointsAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>Dane wydarzenia lub <c>null</c>, gdy wydarzenie nie istnieje.</summary>
    Task<PulseEventInfo?> GetEventAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>Wszystkie wydarzenia (do KPI calego miasta).</summary>
    Task<IReadOnlyList<PulseEventInfo>> GetEventsAsync(CancellationToken cancellationToken = default);

    /// <summary>Wszystkie wydarzenia wraz z punktami PULSE, pobrane jednym zbiorczym odczytem.</summary>
    Task<IReadOnlyList<PulseEventSnapshot>> GetEventsWithPointsAsync(CancellationToken cancellationToken = default);
}
