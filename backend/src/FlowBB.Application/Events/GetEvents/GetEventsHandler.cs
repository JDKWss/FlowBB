using FlowBB.Application.Abstractions.Persistence;

namespace FlowBB.Application.Events.GetEvents;

public sealed class GetEventsHandler(IEventRepository repository)
{
    public Task<IReadOnlyList<EventWithParticipants>> HandleAsync(
        GetEventsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!query.HasValidRange())
        {
            throw new ArgumentException("From cannot be later than To.", nameof(query));
        }

        return repository.ListAsync(query.From, query.To, cancellationToken);
    }
}
