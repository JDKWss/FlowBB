using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Domain.Events;

namespace FlowBB.Application.Events;

/// <summary>Implementacja <see cref="IEventLookup"/> oparta o port odczytu Events, bez dostepu do Neo4j.</summary>
public sealed class EventLookup(IEventRepository repository) : IEventLookup
{
    public async Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await repository.FindAsync(eventId, cancellationToken) is not null;
    }

    public async Task<Event?> FindByIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var found = await repository.FindAsync(eventId, cancellationToken);
        return found?.Event;
    }
}
