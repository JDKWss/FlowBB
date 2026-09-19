using FlowBB.Application.Pulse;

namespace FlowBB.Application.Abstractions.Realtime;

/// <summary>
/// Publikuje aktualizacje PULSE do klientow czasu rzeczywistego (SignalR).
/// Wywolywany dopiero po zatwierdzeniu zapisu Attendance. Nie zawiera danych uzytkownika.
/// </summary>
public interface IPulseNotifier
{
    Task PublishAsync(PulseUpdate update, CancellationToken cancellationToken = default);
}

/// <summary>Zagregowany stan wydarzenia po zmianie Attendance (odpowiada PulseUpdatedMessage z OpenAPI).</summary>
public sealed record PulseUpdate(
    Guid EventId,
    int ParticipantsCount,
    ModalSplit ModalSplit,
    int ParticipantsWithoutReturn,
    DateTimeOffset ChangedAt);
