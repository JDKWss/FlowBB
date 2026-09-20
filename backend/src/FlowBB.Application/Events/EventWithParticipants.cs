using FlowBB.Domain.Events;

namespace FlowBB.Application.Events;

/// <summary>Wydarzenie razem z liczba deklaracji "Ide". Licznik jest wynikiem odczytu, a nie polem modelu domeny.</summary>
public sealed record EventWithParticipants
{
    public EventWithParticipants(Event @event, int participantsCount)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentOutOfRangeException.ThrowIfNegative(participantsCount);

        Event = @event;
        ParticipantsCount = participantsCount;
    }

    public Event Event { get; }

    public int ParticipantsCount { get; }
}
