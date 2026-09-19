namespace FlowBB.Domain.Models;

public sealed record Event(
    string EventId,
    string Title,
    string Description,
    string EventUrl,
    DateTimeOffset DateTime);
