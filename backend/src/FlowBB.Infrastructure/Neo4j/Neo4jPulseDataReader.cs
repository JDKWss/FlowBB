using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Pulse;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

/// <summary>
/// Adapter <see cref="IPulseDataReader"/>. Zwraca surowe punkty snapshotu <c>IS_GOING_TO</c> bez identyfikatora
/// uzytkownika; agregacje (licznik, modal split, heksagony) wykonuje Application. Adapter niczego nie loguje,
/// a komunikaty bledow nie zawieraja wspolrzednych. <c>EndAt</c> wydarzenia jest tylko odczytywane (z zachowaniem offsetu);
/// regule luki powrotowej liczy Application (<c>DemoReturnGapPolicy</c>), nie Cypher.
/// </summary>
public sealed class Neo4jPulseDataReader(IDriver driver, Neo4jOptions options) : IPulseDataReader
{
    // Celowo bez u.UserId: identyfikator uzytkownika nie opuszcza tego zapytania.
    private const string PointsQuery = """
        MATCH (:User)-[r:IS_GOING_TO]->(:Event {EventId: $eventId})
        RETURN r.OriginLatitude AS Latitude, r.OriginLongitude AS Longitude, r.TransportMode AS Mode
        """;

    private const string EventQuery = """
        MATCH (e:Event {EventId: $eventId})
        RETURN e.EventId AS EventId, e.Name AS Name, e.EndAt AS EndAt
        """;

    private const string EventsQuery = """
        MATCH (e:Event)
        RETURN e.EventId AS EventId, e.Name AS Name, e.EndAt AS EndAt
        ORDER BY e.StartAt ASC, e.EventId ASC
        """;

    // Jeden wiersz na deklaracje; HasAttendance odroznia puste wydarzenie od uszkodzonego snapshotu z nullami.
    // Celowo bez u.UserId: identyfikator uzytkownika nie opuszcza zapytania.
    private const string EventsWithPointsQuery = """
        MATCH (e:Event)
        OPTIONAL MATCH (:User)-[r:IS_GOING_TO]->(e)
        RETURN e.EventId AS EventId, e.Name AS Name, e.EndAt AS EndAt,
               r IS NOT NULL AS HasAttendance,
               r.OriginLatitude AS Latitude, r.OriginLongitude AS Longitude, r.TransportMode AS Mode
        """;

    private int executedQueryCount;

    internal int ExecutedQueryCount => Volatile.Read(ref executedQueryCount);

    public async Task<IReadOnlyList<PulsePoint>> GetPointsAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var records = await ReadAsync(PointsQuery, EventIdParameter(eventId), cancellationToken);
        return records.Select(record => MapPoint(record, eventId)).ToList();
    }

    public async Task<PulseEventInfo?> GetEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var records = await ReadAsync(EventQuery, EventIdParameter(eventId), cancellationToken);
        return records.Count == 0 ? null : MapEvent(records[0]);
    }

    public async Task<IReadOnlyList<PulseEventInfo>> GetEventsAsync(CancellationToken cancellationToken = default)
    {
        var records = await ReadAsync(EventsQuery, new Dictionary<string, object?>(), cancellationToken);
        return records.Select(MapEvent).ToList();
    }

    public async Task<IReadOnlyList<PulseEventSnapshot>> GetEventsWithPointsAsync(
        CancellationToken cancellationToken = default)
    {
        var records = await ReadAsync(EventsWithPointsQuery, new Dictionary<string, object?>(), cancellationToken);
        return records
            .GroupBy(record => record["EventId"].As<string>())
            .Select(group => MapSnapshot(group.ToList()))
            .ToList();
    }

    private static Dictionary<string, object?> EventIdParameter(Guid eventId)
    {
        return new Dictionary<string, object?> { ["eventId"] = Neo4jValueConversions.ToDatabaseId(eventId) };
    }

    private async Task<IReadOnlyList<IRecord>> ReadAsync(
        string query,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref executedQueryCount);
        await using var session = driver.AsyncSession(config => config.WithDatabase(options.Database));
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(query, parameters);
            return await cursor.ToListAsync(cancellationToken);
        });
    }

    private static PulsePoint MapPoint(IRecord record, Guid eventId)
    {
        try
        {
            return new PulsePoint(
                record["Latitude"].As<double>(),
                record["Longitude"].As<double>(),
                Neo4jValueConversions.ToTransportModeOrUnknown(record["Mode"]));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidCastException)
        {
            // Bez wyjatku wewnetrznego: jego komunikat zawieralby wartosc wspolrzednej.
            throw new InvalidOperationException($"Event {eventId:D} has an attendance snapshot with invalid coordinates.");
        }
    }

    private static PulseEventInfo MapEvent(IRecord record)
    {
        var id = Neo4jValueConversions.FromDatabaseId(record["EventId"].As<string>());
        var name = record["Name"].As<string?>();

        return string.IsNullOrWhiteSpace(name)
            ? throw new InvalidOperationException($"Event {id:D} in Neo4j has no Name.")
            : new PulseEventInfo(id, name, Neo4jValueConversions.ToNullableDateTimeOffset(record["EndAt"], "EndAt"));
    }

    private static PulseEventSnapshot MapSnapshot(IReadOnlyList<IRecord> records)
    {
        var info = MapEvent(records[0]);
        var points = records
            .Where(record => record["HasAttendance"].As<bool>())
            .Select(record => MapPoint(record, info.Id))
            .ToList();

        return new PulseEventSnapshot(info, points);
    }
}
