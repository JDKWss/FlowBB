using FlowBB.Domain.Events;

namespace FlowBB.Application.Abstractions.Persistence;

/// <summary>Port zapisu wydarzenia i jego dedykowanego miejsca w jednej transakcji.</summary>
public interface IEventWriter
{
    Task CreateAsync(
        Event @event,
        string venueId,
        CancellationToken cancellationToken = default);
}
