using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Pulse;
using FlowBB.Application.Routing;
using FlowBB.Domain.Attendance;
using FlowBB.Domain.Common;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

public sealed class Neo4jAttendanceRepository(
    IDriver driver,
    Neo4jOptions options) : IAttendanceRepository, IAttendanceOriginLookup
{
    private const string ModalSplitProjection = """
        count(attendance) AS ParticipantsCount,
        count(CASE WHEN attendance.TransportMode = 'PublicTransport' THEN 1 END) AS PublicTransport,
        count(CASE WHEN attendance.TransportMode = 'Walking' THEN 1 END) AS Walking,
        count(CASE WHEN attendance.TransportMode = 'Bike' THEN 1 END) AS Bike,
        count(CASE WHEN attendance.TransportMode = 'Car' THEN 1 END) AS Car,
        count(CASE
          WHEN attendance.TransportMode IS NULL
            OR NOT (attendance.TransportMode IN ['PublicTransport', 'Walking', 'Bike', 'Car'])
          THEN 1
        END) AS Unknown
        """;

    public async Task<AttendanceUpsertPersistenceResult?> UpsertAsync(
        AttendanceIntent attendance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attendance);
        cancellationToken.ThrowIfCancellationRequested();

        var query = $$"""
            MATCH (u:User {UserId: $UserId})
            MATCH (e:Event {EventId: $EventId})
            OPTIONAL MATCH (u)-[existing:IS_GOING_TO]->(e)
            WITH u, e, existing IS NULL AS IsNew
            MERGE (u)-[saved:IS_GOING_TO]->(e)
            SET saved.TransportMode = $TransportMode,
                saved.OriginLatitude = u.DefaultOriginLatitude,
                saved.OriginLongitude = u.DefaultOriginLongitude,
                saved.UpdatedAt = datetime($UpdatedAt)
            WITH e, IsNew
            OPTIONAL MATCH (:User)-[attendance:IS_GOING_TO]->(e)
            RETURN IsNew,
                   {{ModalSplitProjection}}
            """;

        await using var session = driver.AsyncSession(config => config.WithDatabase(options.Database));
        return await session.ExecuteWriteAsync(async transaction =>
        {
            var cursor = await transaction.RunAsync(query, new
            {
                UserId = attendance.UserId.ToString("D"),
                EventId = attendance.EventId.ToString("D"),
                TransportMode = attendance.TransportMode.ToString(),
                UpdatedAt = attendance.UpdatedAt.ToString("O")
            });

            if (!await cursor.FetchAsync())
            {
                return null;
            }

            return new AttendanceUpsertPersistenceResult(
                cursor.Current.Get<bool>("IsNew"),
                ToInt(cursor.Current, "ParticipantsCount"),
                MapModalSplit(cursor.Current));
        });
    }

    public async Task<AttendanceDeletePersistenceResult> DeleteAsync(
        Guid eventId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var query = $$"""
            OPTIONAL MATCH (e:Event {EventId: $EventId})
            OPTIONAL MATCH (:User {UserId: $UserId})-[deleted:IS_GOING_TO]->(e)
            WITH e, deleted, deleted IS NOT NULL AS WasDeleted
            DELETE deleted
            WITH e, WasDeleted
            OPTIONAL MATCH (:User)-[attendance:IS_GOING_TO]->(e)
            RETURN WasDeleted,
                   {{ModalSplitProjection}}
            """;

        await using var session = driver.AsyncSession(config => config.WithDatabase(options.Database));
        return await session.ExecuteWriteAsync(async transaction =>
        {
            var cursor = await transaction.RunAsync(query, new
            {
                EventId = eventId.ToString("D"),
                UserId = userId.ToString("D")
            });
            await cursor.FetchAsync();

            return new AttendanceDeletePersistenceResult(
                cursor.Current.Get<bool>("WasDeleted"),
                ToInt(cursor.Current, "ParticipantsCount"),
                MapModalSplit(cursor.Current));
        });
    }

    public async Task<AttendanceOrigin?> FindAsync(
        Guid eventId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            MATCH (:User {UserId: $UserId})-[attendance:IS_GOING_TO]->(:Event {EventId: $EventId})
            RETURN attendance.OriginLatitude AS Latitude,
                   attendance.OriginLongitude AS Longitude,
                   attendance.TransportMode AS TransportMode
            """;

        cancellationToken.ThrowIfCancellationRequested();
        var result = await driver.ExecutableQuery(query)
            .WithParameters(new
            {
                EventId = eventId.ToString("D"),
                UserId = userId.ToString("D")
            })
            .WithConfig(new QueryConfig(database: options.Database, routing: RoutingControl.Readers))
            .ExecuteAsync(cancellationToken);

        var record = result.Result.SingleOrDefault();
        if (record is null || record["Latitude"] is null || record["Longitude"] is null)
        {
            return null;
        }

        return new AttendanceOrigin(
            new GeoPoint(record.Get<double>("Latitude"), record.Get<double>("Longitude")),
            ParseTransportMode(record.Get<string?>("TransportMode")));
    }

    private static ModalSplit MapModalSplit(IRecord record)
    {
        return new ModalSplit(
            ToInt(record, "PublicTransport"),
            ToInt(record, "Walking"),
            ToInt(record, "Bike"),
            ToInt(record, "Car"),
            ToInt(record, "Unknown"));
    }

    private static int ToInt(IRecord record, string key) => checked((int)record.Get<long>(key));

    private static TransportMode ParseTransportMode(string? value)
    {
        return Enum.TryParse<TransportMode>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : TransportMode.Unknown;
    }
}
