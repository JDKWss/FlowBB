using System.Globalization;
using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Events;
using FlowBB.Domain.Common;
using FlowBB.Domain.Events;
using Neo4j.Driver;
using DomainEvent = FlowBB.Domain.Events.Event;

namespace FlowBB.Infrastructure.Neo4j;

public sealed class Neo4jEventRepository(
    IDriver driver,
    Neo4jOptions options) : IEventRepository
{
    private const string EventProjection = """
        e.EventId AS EventId,
        e.Name AS Name,
        e.Description AS Description,
        toString(e.StartAt) AS StartAt,
        toString(e.EndAt) AS EndAt,
        e.Category AS Category,
        e.Source AS Source,
        venue.Name AS VenueName,
        venue.Latitude AS Latitude,
        venue.Longitude AS Longitude,
        count(attendance) AS ParticipantsCount
        """;

    public async Task<IReadOnlyList<EventWithParticipants>> ListAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var query = $$"""
            MATCH (e:Event)-[:HOSTED_AT]->(venue:Venue)
            WHERE ($From IS NULL OR e.StartAt >= datetime($From))
              AND ($To IS NULL OR e.StartAt <= datetime($To))
            OPTIONAL MATCH (:User)-[attendance:IS_GOING_TO]->(e)
            RETURN {{EventProjection}}
            ORDER BY StartAt, EventId
            """;

        var records = await ReadAsync(query, new
        {
            From = from?.ToString("O"),
            To = to?.ToString("O")
        }, cancellationToken);
        return records.Select(MapEvent).ToList();
    }

    public async Task<EventWithParticipants?> FindAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var query = $$"""
            MATCH (e:Event {EventId: $EventId})-[:HOSTED_AT]->(venue:Venue)
            OPTIONAL MATCH (:User)-[attendance:IS_GOING_TO]->(e)
            RETURN {{EventProjection}}
            """;

        var records = await ReadAsync(query, new { EventId = eventId.ToString("D") }, cancellationToken);
        var record = records.SingleOrDefault();
        return record is null ? null : MapEvent(record);
    }

    private async Task<IReadOnlyList<IRecord>> ReadAsync(
        string query,
        object parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await driver.ExecutableQuery(query)
            .WithParameters(parameters)
            .WithConfig(new QueryConfig(database: options.Database, routing: RoutingControl.Readers))
            .ExecuteAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return result.Result;
    }

    private static EventWithParticipants MapEvent(IRecord record)
    {
        var @event = new DomainEvent(
            Guid.ParseExact(record.Get<string>("EventId"), "D"),
            record.Get<string>("Name"),
            record.Get<string>("Description"),
            ParseInstant(record.Get<string>("StartAt")),
            ParseOptionalInstant(record.Get<string?>("EndAt")),
            record.Get<string>("VenueName"),
            ParseEnum<EventCategory>(record.Get<string>("Category"), "Category"),
            ParseEnum<EventSource>(record.Get<string>("Source"), "Source"),
            new GeoPoint(record.Get<double>("Latitude"), record.Get<double>("Longitude")));

        return new EventWithParticipants(
            @event,
            checked((int)record.Get<long>("ParticipantsCount")));
    }

    private static DateTimeOffset ParseInstant(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    private static DateTimeOffset? ParseOptionalInstant(string? value)
    {
        return value is null ? null : ParseInstant(value);
    }

    private static TEnum ParseEnum<TEnum>(string value, string propertyName)
        where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Neo4j Event.{propertyName} has unsupported value '{value}'.");
    }
}
