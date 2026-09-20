using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Routing;
using FlowBB.Domain.Common;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

/// <summary>Adapter <see cref="IAttendanceOriginLookup"/>: czyta snapshot relacji <c>IS_GOING_TO</c>.</summary>
public sealed class Neo4jAttendanceOriginLookup(IDriver driver, Neo4jOptions options) : IAttendanceOriginLookup
{
    private const string Query = """
        MATCH (:User {UserId: $userId})-[r:IS_GOING_TO]->(:Event {EventId: $eventId})
        RETURN r.TransportMode AS Mode, r.OriginLatitude AS Latitude, r.OriginLongitude AS Longitude
        """;

    public async Task<AttendanceOrigin?> FindAsync(
        Guid eventId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["eventId"] = Neo4jValueConversions.ToDatabaseId(eventId),
            ["userId"] = Neo4jValueConversions.ToDatabaseId(userId)
        };

        await using var session = driver.AsyncSession(config => config.WithDatabase(options.Database));
        var records = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(Query, parameters);
            return await cursor.ToListAsync(cancellationToken);
        });

        return records.Count == 0 ? null : MapOrigin(records[0], eventId, userId);
    }

    private static AttendanceOrigin MapOrigin(IRecord record, Guid eventId, Guid userId)
    {
        try
        {
            return new AttendanceOrigin(
                new GeoPoint(record["Latitude"].As<double>(), record["Longitude"].As<double>()),
                Neo4jValueConversions.ToEnum<TransportMode>(record["Mode"], "TransportMode"));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidCastException)
        {
            // Komunikat nie zawiera wspolrzednych: to dane wewnetrzne, ktore nie moga trafic do logow.
            throw new InvalidOperationException(
                $"Attendance snapshot of user {userId:D} for event {eventId:D} is invalid.");
        }
    }
}
