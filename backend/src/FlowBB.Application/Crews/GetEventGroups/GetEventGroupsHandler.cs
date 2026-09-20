using FlowBB.Application.Abstractions.Persistence;

namespace FlowBB.Application.Crews.GetEventGroups;

public sealed class GetEventGroupsHandler(ICrewRepository crews, IEventLookup events)
{
    /// <returns>Grupy wydarzenia albo <c>null</c>, gdy wydarzenie nie istnieje.</returns>
    public async Task<IReadOnlyList<CrewSummary>?> HandleAsync(
        Guid eventId, Guid? userId, CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event id cannot be an empty GUID.", nameof(eventId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be an empty GUID.", nameof(userId));
        }

        if (!await events.ExistsAsync(eventId, cancellationToken))
        {
            return null;
        }

        return await crews.ListByEventAsync(eventId, userId, cancellationToken);
    }
}
