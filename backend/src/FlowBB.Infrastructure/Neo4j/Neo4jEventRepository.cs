using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Events;
using FlowBB.Domain.Common;
using FlowBB.Domain.Events;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

/// <summary>
/// Adapter <see cref="IEventRepository"/>. Liczba uczestnikow jest wyliczana z relacji <c>IS_GOING_TO</c>.
/// Wydarzenie bez miejsca (<c>HOSTED_AT</c>) nie jest zwracane: domena wymaga nazwy miejsca i lokalizacji.
/// </summary>
public sealed class Neo4jEventRepository(IDriver driver, Neo4jOptions options) : IEventRepository
{
    private const string EventProjection = """
        MATCH (e:Event)-[:HOSTED_AT]->(v:Venue)
        WHERE {0}
        OPTIONAL MATCH (:User)-[g:IS_GOING_TO]->(e)
        WITH e, v, count(g) AS ParticipantsCount
        RETURN e.EventId AS EventId, e.Name AS Name, coalesce(e.Description, '') AS Description,
               e.StartAt AS StartAt, e.EndAt AS EndAt, e.Category AS Category, e.Source AS Source,
               v.Name AS VenueName, v.Latitude AS Latitude, v.Longitude AS Longitude, ParticipantsCount
        ORDER BY e.StartAt ASC, e.EventId ASC
        """;

    private static readonly string ListQuery = string.Format(
        EventProjection,
        "($from IS NULL OR e.StartAt >= $from) AND ($to IS NULL OR e.StartAt <= $to)");

    private static readonly string FindQuery = string.Format(EventProjection, "e.EventId = $eventId");

    public async Task<IReadOnlyList<EventWithParticipants>> ListAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object?> { ["from"] = from, ["to"] = to };
        var records = await ReadAsync(ListQuery, parameters, cancellationToken);
        return records.Select(MapEvent).ToList();
    }

    public async Task<EventWithParticipants?> FindAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object?> { ["eventId"] = Neo4jValueConversions.ToDatabaseId(eventId) };
        var records = await ReadAsync(FindQuery, parameters, cancellationToken);
        return records.Count == 0 ? null : MapEvent(records[0]);
    }

    private async Task<IReadOnlyList<IRecord>> ReadAsync(
        string query,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        await using var session = driver.AsyncSession(config => config.WithDatabase(options.Database));
        return await session.ExecuteReadAsync(
            async tx =>
            {
                var cursor = await tx.RunAsync(query, parameters);
                return await cursor.ToListAsync(cancellationToken);
            });
    }

    private static EventWithParticipants MapEvent(IRecord record)
    {
        var id = Neo4jValueConversions.FromDatabaseId(record["EventId"].As<string>());

        try
        {
            var @event = new Event(
                id,
                record["Name"].As<string>(),
                record["Description"].As<string>(),
                Neo4jValueConversions.ToDateTimeOffset(record["StartAt"], "StartAt"),
                Neo4jValueConversions.ToNullableDateTimeOffset(record["EndAt"], "EndAt"),
                record["VenueName"].As<string>(),
                Neo4jValueConversions.ToEnum<EventCategory>(record["Category"], "Category"),
                Neo4jValueConversions.ToEnum<EventSource>(record["Source"], "Source"),
                new GeoPoint(record["Latitude"].As<double>(), record["Longitude"].As<double>()));

            return new EventWithParticipants(@event, Convert.ToInt32(record["ParticipantsCount"].As<long>()));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidCastException)
        {
            throw new InvalidOperationException($"Event {id:D} in Neo4j violates the data contract: {ex.Message}", ex);
        }
    }
}
