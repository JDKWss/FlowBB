namespace FlowBB.Application.Pulse;

/// <summary>
/// Podstawowe dane wydarzenia potrzebne widokom PULSE (nazwa do wyswietlenia i koniec wydarzenia).
/// <paramref name="EndAt"/> to wejscie reguly luki powrotowej (docs/code/MODULE_PULSE.md); <c>null</c> oznacza brak luki.
/// </summary>
public sealed record PulseEventInfo(Guid Id, string Name, DateTimeOffset? EndAt = null);
