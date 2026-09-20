using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Domain.Events;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

/// <summary>Tworzy Event, dedykowany Venue i relacje HOSTED_AT atomowo.</summary>
public sealed class Neo4jEventWriter(IDriver driver, Neo4jOptions options) : IEventWriter
{
    private const string CreateQuery = """
        CREATE (v:Venue {
            VenueId: $venueId,
            Name: $venueName,
            Address: $venueName,
            Latitude: $latitude,
            Longitude: $longitude
        })
        CREATE (e:Event {
            EventId: $eventId,
            Name: $name,
            Description: $description,
            StartAt: $startAt,
            EndAt: $endAt,
            EventUrl: $eventUrl,
            Category: $category,
            Source: $source
        })
        CREATE (e)-[:HOSTED_AT]->(v)
        RETURN e.EventId
        """;

    public async Task CreateAsync(
        Event @event,
        string venueId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(venueId);

        var parameters = new Dictionary<string, object?>
        {
            ["venueId"] = venueId,
            ["venueName"] = @event.VenueName,
            ["latitude"] = @event.Location.Latitude,
            ["longitude"] = @event.Location.Longitude,
            ["eventId"] = Neo4jValueConversions.ToDatabaseId(@event.Id),
            ["name"] = @event.Name,
            ["description"] = @event.Description,
            ["startAt"] = @event.StartAt,
            ["endAt"] = @event.EndAt,
            ["eventUrl"] = $"urn:flowbb:event:{@event.Id:D}",
            ["category"] = @event.Category.ToString(),
            ["source"] = @event.Source.ToString()
        };

        await using var session = driver.AsyncSession(config => config.WithDatabase(options.Database));
        await session.ExecuteWriteAsync(async transaction =>
        {
            var cursor = await transaction.RunAsync(CreateQuery, parameters);
            await cursor.ToListAsync(cancellationToken);
        });
    }
}
