using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Domain.Events;

namespace FlowBB.Api.IntegrationTests.Infrastructure;

/// <summary>Fake portu <see cref="IEventLookup"/>: zna tylko wskazane identyfikatory wydarzen.</summary>
public sealed class FakeEventLookup(params Guid[] knownEventIds) : IEventLookup
{
    public Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult(knownEventIds.Contains(eventId));

    public Task<Event?> FindByIdAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Event?>(null);
}
