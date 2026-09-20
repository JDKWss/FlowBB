using FlowBB.Domain.Events;
using FlowBB.Infrastructure.Neo4j;
using FluentAssertions;

namespace FlowBB.Infrastructure.Tests.Neo4j;

[Collection(Neo4jCollection.Name)]
public sealed class Neo4jEventRepositoryTests(Neo4jFixture neo4j)
{
    // Okno w odleglej przyszlosci, losowe dla kazdego testu, zeby nie zderzyc sie z seedem ani z innymi testami.
    private static DateTimeOffset NewWindowStart() =>
        new DateTimeOffset(2100, 1, 1, 8, 0, 0, TimeSpan.FromHours(2)).AddDays(Random.Shared.Next(0, 3000));

    private Neo4jEventRepository CreateRepository() => new(neo4j.Driver!, neo4j.Options!);

    [Neo4jFact]
    public async Task FindAsync_MapsEventVenueAndEnums()
    {
        var start = NewWindowStart();
        var venueId = await neo4j.CreateVenueAsync("Rynek", 49.82245, 19.04431);
        var eventId = await neo4j.CreateEventAsync(venueId, start, start.AddHours(3), "Koncert", "Sport", "City");

        var found = await CreateRepository().FindAsync(eventId);

        found.Should().NotBeNull();
        found!.Event.Id.Should().Be(eventId);
        found.Event.Name.Should().Be("Koncert");
        found.Event.Description.Should().Be("Opis");
        found.Event.VenueName.Should().Be("Rynek");
        found.Event.Category.Should().Be(EventCategory.Sport);
        found.Event.Source.Should().Be(EventSource.City);
        found.Event.Location.Latitude.Should().Be(49.82245);
        found.Event.Location.Longitude.Should().Be(19.04431);
        found.Event.StartAt.Should().Be(start);
        found.Event.EndAt.Should().Be(start.AddHours(3));
        found.ParticipantsCount.Should().Be(0);
    }

    [Neo4jFact]
    public async Task FindAsync_KeepsNullEndAt()
    {
        var venueId = await neo4j.CreateVenueAsync();
        var eventId = await neo4j.CreateEventAsync(venueId, NewWindowStart());

        var found = await CreateRepository().FindAsync(eventId);

        found!.Event.EndAt.Should().BeNull();
    }

    [Neo4jFact]
    public async Task FindAsync_ReturnsNullForUnknownEvent()
    {
        var found = await CreateRepository().FindAsync(Guid.NewGuid());

        found.Should().BeNull();
    }

    [Neo4jFact]
    public async Task FindAsync_IgnoresEventWithoutVenue()
    {
        var eventId = Guid.NewGuid();
        await neo4j.ExecuteAsync(
            "CREATE (:Event {EventId: $id, Name: 'Bez miejsca', StartAt: datetime(), Category: 'Other', Source: 'Demo', TestRunId: 'orphan'})",
            new { id = eventId.ToString("D") });

        try
        {
            var found = await CreateRepository().FindAsync(eventId);

            found.Should().BeNull();
        }
        finally
        {
            await neo4j.ExecuteAsync("MATCH (e:Event {EventId: $id}) DETACH DELETE e", new { id = eventId.ToString("D") });
        }
    }

    [Neo4jFact]
    public async Task ListAsync_CountsParticipantsFromIsGoingToRelations()
    {
        var start = NewWindowStart();
        var venueId = await neo4j.CreateVenueAsync();
        var popular = await neo4j.CreateEventAsync(venueId, start);
        var empty = await neo4j.CreateEventAsync(venueId, start.AddHours(1));
        await neo4j.DeclareGoingAsync(await neo4j.CreateUserAsync(), popular);
        await neo4j.DeclareGoingAsync(await neo4j.CreateUserAsync(), popular);

        var list = await CreateRepository().ListAsync(start.AddMinutes(-1), start.AddHours(2));

        list.Single(item => item.Event.Id == popular).ParticipantsCount.Should().Be(2);
        list.Single(item => item.Event.Id == empty).ParticipantsCount.Should().Be(0);
    }

    [Neo4jFact]
    public async Task ListAsync_OrdersByStartAtAndFiltersInclusiveWindow()
    {
        var start = NewWindowStart();
        var venueId = await neo4j.CreateVenueAsync();
        var late = await neo4j.CreateEventAsync(venueId, start.AddHours(2));
        var early = await neo4j.CreateEventAsync(venueId, start);
        var outside = await neo4j.CreateEventAsync(venueId, start.AddHours(3));
        var middle = await neo4j.CreateEventAsync(venueId, start.AddHours(1));

        var list = await CreateRepository().ListAsync(start, start.AddHours(2));

        list.Select(item => item.Event.Id).Should().Equal(early, middle, late);
        list.Select(item => item.Event.Id).Should().NotContain(outside);
    }

    [Neo4jFact]
    public async Task ListAsync_WithoutBoundsReturnsOwnEvents()
    {
        var venueId = await neo4j.CreateVenueAsync();
        var eventId = await neo4j.CreateEventAsync(venueId, NewWindowStart());

        var list = await CreateRepository().ListAsync(null, null);

        list.Select(item => item.Event.Id).Should().Contain(eventId);
    }

    [Neo4jFact]
    public async Task FindAsync_ThrowsReadableErrorForUnknownCategory()
    {
        var venueId = await neo4j.CreateVenueAsync();
        var eventId = await neo4j.CreateEventAsync(venueId, NewWindowStart(), category: "1");

        try
        {
            var act = () => CreateRepository().FindAsync(eventId);

            (await act.Should().ThrowAsync<InvalidOperationException>())
                .WithMessage($"*{eventId:D}*Category*");
        }
        finally
        {
            // Uszkodzone wydarzenie nie moze zostac w bazie: psulby ListAsync w pozostalych testach.
            await neo4j.ExecuteAsync("MATCH (e:Event {EventId: $id}) DETACH DELETE e", new { id = eventId.ToString("D") });
        }
    }
}
