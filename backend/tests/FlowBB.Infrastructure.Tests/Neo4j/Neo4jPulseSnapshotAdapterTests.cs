using FlowBB.Application.Pulse;
using FlowBB.Domain.Attendance;
using FlowBB.Domain.Common;
using FlowBB.Infrastructure.Neo4j;
using FluentAssertions;

namespace FlowBB.Infrastructure.Tests.Neo4j;

[Collection(Neo4jCollection.Name)]
public sealed class Neo4jPulseSnapshotAdapterTests(Neo4jFixture neo4j)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);

    private Neo4jPulseDataReader CreateReader() => new(neo4j.Driver!, neo4j.Options!);

    private Neo4jAttendanceRepository CreateAttendance() => new(neo4j.Driver!, neo4j.Options!);

    private async Task<Guid> CreateEventAsync(string name = "Test Event", int daysFromNow = 0)
    {
        var venueId = await neo4j.CreateVenueAsync();
        var start = new DateTimeOffset(2100, 1, 1, 8, 0, 0, TimeSpan.Zero).AddDays(daysFromNow);
        return await neo4j.CreateEventAsync(venueId, start, name: name);
    }

    [Neo4jFact]
    public async Task GetPointsAsync_ReturnsSnapshotPointsOfThisEventOnly()
    {
        var eventId = await CreateEventAsync();
        var otherEvent = await CreateEventAsync();
        var walker = await neo4j.CreateUserAsync(49.8155, 19.0340);
        var driver = await neo4j.CreateUserAsync(49.8330, 19.0520);
        var attendance = CreateAttendance();
        await attendance.UpsertAsync(new AttendanceIntent(eventId, walker, TransportMode.Walking, Now));
        await attendance.UpsertAsync(new AttendanceIntent(eventId, driver, TransportMode.Car, Now));
        await attendance.UpsertAsync(new AttendanceIntent(otherEvent, walker, TransportMode.Bike, Now));

        var points = await CreateReader().GetPointsAsync(eventId);

        points.Should().BeEquivalentTo(
            new[]
            {
                new PulsePoint(49.8155, 19.0340, TransportMode.Walking),
                new PulsePoint(49.8330, 19.0520, TransportMode.Car)
            });
    }

    [Neo4jFact]
    public async Task GetPointsAsync_UsesSnapshotNotCurrentHome()
    {
        var eventId = await CreateEventAsync();
        var userId = await neo4j.CreateUserAsync(49.8155, 19.0340);
        await CreateAttendance().UpsertAsync(new AttendanceIntent(eventId, userId, TransportMode.Bike, Now));
        await neo4j.ExecuteAsync(
            "MATCH (u:User {UserId: $id}) SET u.HomeLatitude = 50.0, u.HomeLongitude = 20.0",
            new { id = userId.ToString("D") });

        var points = await CreateReader().GetPointsAsync(eventId);

        points.Should().ContainSingle().Which.Should().Be(new PulsePoint(49.8155, 19.0340, TransportMode.Bike));
    }

    [Neo4jFact]
    public async Task GetPointsAsync_ReturnsEmptyListWhenNobodyIsGoing()
    {
        var eventId = await CreateEventAsync();

        var points = await CreateReader().GetPointsAsync(eventId);

        points.Should().BeEmpty();
    }

    [Neo4jFact]
    public async Task GetPointsAsync_MatchesUpsertModalSplitAndCount()
    {
        var eventId = await CreateEventAsync();
        var attendance = CreateAttendance();
        var last = (Participants: 0, Split: new ModalSplit(0, 0, 0, 0, 0));
        foreach (var mode in new[] { TransportMode.Walking, TransportMode.Car, TransportMode.Car })
        {
            var userId = await neo4j.CreateUserAsync();
            var result = await attendance.UpsertAsync(new AttendanceIntent(eventId, userId, mode, Now));
            last = (result!.ParticipantsCount, result.ModalSplit);
        }

        var points = await CreateReader().GetPointsAsync(eventId);

        points.Should().HaveCount(last.Participants);
        ModalSplit.From(points).Should().Be(last.Split);
    }

    [Neo4jFact]
    public async Task GetPointsAsync_UnknownModeCountsAsUnknown()
    {
        var eventId = await CreateEventAsync();
        var userId = await neo4j.CreateUserAsync();
        await neo4j.ExecuteAsync(
            """
            MATCH (u:User {UserId: $u}), (e:Event {EventId: $e})
            CREATE (u)-[:IS_GOING_TO {TransportMode: 'Teleport', OriginLatitude: 49.8, OriginLongitude: 19.0, UpdatedAt: datetime()}]->(e)
            """,
            new { u = userId.ToString("D"), e = eventId.ToString("D") });

        var points = await CreateReader().GetPointsAsync(eventId);

        points.Should().ContainSingle().Which.TransportMode.Should().Be(TransportMode.Unknown);
    }

    [Neo4jFact]
    public async Task GetPointsAsync_InvalidCoordinates_ThrowsWithoutLeakingThem()
    {
        var eventId = await CreateEventAsync();
        var userId = await neo4j.CreateUserAsync();
        await neo4j.ExecuteAsync(
            """
            MATCH (u:User {UserId: $u}), (e:Event {EventId: $e})
            CREATE (u)-[:IS_GOING_TO {TransportMode: 'Car', OriginLatitude: 91.234, OriginLongitude: 19.0, UpdatedAt: datetime()}]->(e)
            """,
            new { u = userId.ToString("D"), e = eventId.ToString("D") });

        var act = () => CreateReader().GetPointsAsync(eventId);

        var thrown = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        thrown.Message.Should().Contain(eventId.ToString("D")).And.NotContain("91.234");
        thrown.Message.Should().NotContain(userId.ToString("D"));
        thrown.InnerException.Should().BeNull();
    }

    [Neo4jFact]
    public async Task GetEventAsync_ReturnsNameOrNull()
    {
        var eventId = await CreateEventAsync("Koncert PULSE");

        var found = await CreateReader().GetEventAsync(eventId);
        var missing = await CreateReader().GetEventAsync(Guid.NewGuid());

        found.Should().Be(new PulseEventInfo(eventId, "Koncert PULSE"));
        missing.Should().BeNull();
    }

    [Neo4jFact]
    public async Task GetEventsAsync_ReturnsAllEventsOrderedByStartAt()
    {
        var late = await CreateEventAsync("Pozny", daysFromNow: 20);
        var early = await CreateEventAsync("Wczesny", daysFromNow: 10);

        var events = await CreateReader().GetEventsAsync();

        var ids = events.Select(item => item.Id).ToList();
        ids.Should().Contain([early, late]);
        ids.IndexOf(early).Should().BeLessThan(ids.IndexOf(late));
    }
}
