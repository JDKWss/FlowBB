namespace FlowBB.Domain.Models;

public sealed record Event(
    Guid EventId,
    string Name,
    string Description,
    string EventUrl,
    DateTimeOffset StartAt,
    DateTimeOffset? EndAt);
