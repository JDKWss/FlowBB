using FlowBB.Application.Pulse.GetActivityMap;
using FlowBB.Infrastructure.Neo4j;
using FluentAssertions;

namespace FlowBB.Infrastructure.Tests.Neo4j;

/// <summary>
/// Prog prywatnosci PULSE na danych z prawdziwej bazy: komorka z 9 osobami jest ukryta, z 10 zwracana
/// (AGENTS.md, sekcja 8). Licznik calkowity liczy wszystkich, takze tych z ukrytej komorki.
/// </summary>
[Collection(Neo4jCollection.Name)]
public sealed class Neo4jPulsePrivacyThresholdTests(Neo4jFixture neo4j)
{
    private const double Latitude = 49.8225;
    private const double Longitude = 19.0444;

    [Neo4jFact]
    public async Task ActivityMap_CellWithNineParticipantsFromDatabase_IsHidden()
    {
        var eventId = await CreateEventWithParticipantsAsync(GetActivityMapHandler.MinCellParticipants - 1);

        var map = await CreateHandler().HandleAsync(eventId);

        map.Cells.Should().BeEmpty();
        map.ParticipantsCount.Should().Be(GetActivityMapHandler.MinCellParticipants - 1);
    }

    [Neo4jFact]
    public async Task ActivityMap_CellWithTenParticipantsFromDatabase_IsReturned()
    {
        var eventId = await CreateEventWithParticipantsAsync(GetActivityMapHandler.MinCellParticipants);

        var map = await CreateHandler().HandleAsync(eventId);

        map.Cells.Should().ContainSingle().Which.Participants.Should().Be(GetActivityMapHandler.MinCellParticipants);
    }

    [Neo4jFact]
    public async Task ActivityMap_LeavingTheTenthParticipant_HidesTheCellAgain()
    {
        var eventId = await CreateEventWithParticipantsAsync(GetActivityMapHandler.MinCellParticipants);
        await neo4j.ExecuteAsync(
            "MATCH (u:User {TestRunId: $runId})-[r:IS_GOING_TO]->(:Event {EventId: $eventId}) WITH r LIMIT 1 DELETE r",
            new { runId = neo4j.RunId, eventId = eventId.ToString("D") });

        var map = await CreateHandler().HandleAsync(eventId);

        map.Cells.Should().BeEmpty();
    }

    private GetActivityMapHandler CreateHandler() => new(new Neo4jPulseDataReader(neo4j.Driver!, neo4j.Options!));

    private async Task<Guid> CreateEventWithParticipantsAsync(int participants)
    {
        var venueId = await neo4j.CreateVenueAsync();
        var eventId = await neo4j.CreateEventAsync(venueId, new DateTimeOffset(2100, 1, 1, 8, 0, 0, TimeSpan.Zero));
        for (var index = 0; index < participants; index++)
        {
            var userId = await neo4j.CreateUserAsync(Latitude, Longitude);
            await neo4j.ExecuteAsync(
                """
                MATCH (u:User {UserId: $userId}), (e:Event {EventId: $eventId})
                CREATE (u)-[:IS_GOING_TO {TransportMode: 'Walking', OriginLatitude: $latitude,
                                          OriginLongitude: $longitude, UpdatedAt: datetime()}]->(e)
                """,
                new { userId = userId.ToString("D"), eventId = eventId.ToString("D"), latitude = Latitude, longitude = Longitude });
        }

        return eventId;
    }
}
