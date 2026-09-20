using FlowBB.Domain.Common;
using FlowBB.Domain.Events;
using FlowBB.Infrastructure.Neo4j;
using FluentAssertions;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Tests.Neo4j;

[Collection(Neo4jCollection.Name)]
public sealed class Neo4jEventWriterTests(Neo4jFixture neo4j)
{
    [Neo4jFact]
    public async Task CreateAsync_PersistsEventVenueAndHostedAtWithoutParticipantCounter()
    {
        var eventId = Guid.NewGuid();
        var venueId = $"external-venue-test-{Guid.NewGuid():N}";
        var @event = new Event(
            eventId,
            "FlowBB Demo Event",
            "Created by an organizer.",
            new DateTimeOffset(2026, 9, 20, 19, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 9, 20, 22, 0, 0, TimeSpan.FromHours(2)),
            "Plac Bolesława Chrobrego",
            EventCategory.Community,
            EventSource.External,
            new GeoPoint(49.8215, 19.0455));
        var writer = new Neo4jEventWriter(neo4j.Driver!, neo4j.Options!);

        try
        {
            await writer.CreateAsync(@event, venueId);

            var records = await neo4j.QueryAsync(
                """
                MATCH (e:Event {EventId: $eventId})-[r:HOSTED_AT]->(v:Venue {VenueId: $venueId})
                OPTIONAL MATCH (:User)-[attendance:IS_GOING_TO]->(e)
                RETURN e.Source AS Source, e.Category AS Category, e.ParticipantsCount AS StoredParticipants,
                       v.Name AS VenueName, v.Latitude AS Latitude, v.Longitude AS Longitude,
                       count(attendance) AS Participants, count(r) AS HostedAt
                """,
                new { eventId = eventId.ToString("D"), venueId });

            var record = records.Should().ContainSingle().Subject;
            global::Neo4j.Driver.ValueExtensions.As<string>(record["Source"]).Should().Be("External");
            global::Neo4j.Driver.ValueExtensions.As<string>(record["Category"]).Should().Be("Community");
            record["StoredParticipants"].Should().BeNull();
            global::Neo4j.Driver.ValueExtensions.As<string>(record["VenueName"]).Should().Be("Plac Bolesława Chrobrego");
            global::Neo4j.Driver.ValueExtensions.As<double>(record["Latitude"]).Should().Be(49.8215);
            global::Neo4j.Driver.ValueExtensions.As<double>(record["Longitude"]).Should().Be(19.0455);
            global::Neo4j.Driver.ValueExtensions.As<long>(record["Participants"]).Should().Be(0);
            global::Neo4j.Driver.ValueExtensions.As<long>(record["HostedAt"]).Should().Be(1);
        }
        finally
        {
            await neo4j.ExecuteAsync(
                """
                MATCH (n)
                WHERE (n:Event AND n.EventId = $eventId) OR (n:Venue AND n.VenueId = $venueId)
                DETACH DELETE n
                """,
                new { eventId = eventId.ToString("D"), venueId });
        }
    }
}
