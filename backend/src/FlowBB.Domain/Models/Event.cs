namespace FlowBB.Domain.Models;

/// <summary>
/// Superseded by <see cref="FlowBB.Domain.Events.Event"/>. Kept only until the Neo4j Events adapter migrates to the new model.
/// </summary>
public sealed record Event(
    Guid EventId,
    string Name,
    string Description,
    string EventUrl,
    DateTimeOffset StartAt,
    DateTimeOffset? EndAt);
