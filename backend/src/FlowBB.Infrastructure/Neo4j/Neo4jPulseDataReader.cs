using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Pulse;
using FlowBB.Domain.Common;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

public sealed class Neo4jPulseDataReader(
    IDriver driver,
    Neo4jOptions options) : IPulseDataReader
{
    public async Task<IReadOnlyList<PulsePoint>> GetPointsAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            MATCH (:User)-[attendance:IS_GOING_TO]->(:Event {EventId: $EventId})
            WHERE attendance.OriginLatitude IS NOT NULL
              AND attendance.OriginLongitude IS NOT NULL
            RETURN attendance.OriginLatitude AS Latitude,
                   attendance.OriginLongitude AS Longitude,
                   attendance.TransportMode AS TransportMode
            """;

        var records = await ReadAsync(query, new { EventId = eventId.ToString("D") }, cancellationToken);
        return records.Select(MapPoint).ToList();
    }

    public async Task<PulseEventInfo?> GetEventAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            MATCH (e:Event {EventId: $EventId})
            RETURN e.EventId AS EventId, e.Name AS Name
            """;

        var records = await ReadAsync(query, new { EventId = eventId.ToString("D") }, cancellationToken);
        var record = records.SingleOrDefault();
        return record is null ? null : MapEvent(record);
    }

    public async Task<IReadOnlyList<PulseEventInfo>> GetEventsAsync(
        CancellationToken cancellationToken = default)
    {
        const string query = """
            MATCH (e:Event)
            RETURN e.EventId AS EventId, e.Name AS Name
            ORDER BY e.StartAt, e.EventId
            """;

        var records = await ReadAsync(query, new { }, cancellationToken);
        return records.Select(MapEvent).ToList();
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

    private static PulseEventInfo MapEvent(IRecord record)
    {
        return new PulseEventInfo(
            Guid.ParseExact(record.Get<string>("EventId"), "D"),
            record.Get<string>("Name"));
    }

    private static PulsePoint MapPoint(IRecord record)
    {
        var latitude = record.Get<double>("Latitude");
        var longitude = record.Get<double>("Longitude");
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90 ||
            !double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            throw new InvalidOperationException("Neo4j attendance snapshot contains invalid coordinates.");
        }

        return new PulsePoint(
            latitude,
            longitude,
            ParseTransportMode(record.Get<string?>("TransportMode")));
    }

    private static TransportMode ParseTransportMode(string? value)
    {
        return Enum.TryParse<TransportMode>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : TransportMode.Unknown;
    }
}
