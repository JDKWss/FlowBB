using FlowBB.Application.Abstractions.Persistence;

namespace FlowBB.Application.Events.GetEvent;

public sealed class GetEventHandler(IEventRepository repository)
{
    /// <returns>Szczegoly wydarzenia albo <c>null</c>, gdy wydarzenie nie istnieje.</returns>
    public async Task<EventDetails?> HandleAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event id cannot be an empty GUID.", nameof(eventId));
        }

        var found = await repository.FindAsync(eventId, cancellationToken);
        return found is null ? null : EventDetails.From(found);
    }
}
