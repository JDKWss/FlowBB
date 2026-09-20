using FlowBB.Application.Pulse;
using FlowBB.Domain.Attendance;
using FlowBB.Domain.Common;
using FlowBB.Infrastructure.Neo4j;
using FluentAssertions;

namespace FlowBB.Infrastructure.Tests.Neo4j;

[Collection(Neo4jCollection.Name)]
public sealed class Neo4jAttendanceRepositoryTests(Neo4jFixture neo4j)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);

    private Neo4jAttendanceRepository CreateRepository() => new(neo4j.Driver!, neo4j.Options!);

    private Neo4jAttendanceOriginLookup CreateLookup() => new(neo4j.Driver!, neo4j.Options!);

    private async Task<Guid> CreateEventAsync()
    {
        var venueId = await neo4j.CreateVenueAsync();
        return await neo4j.CreateEventAsync(venueId, new DateTimeOffset(2100, 1, 1, 8, 0, 0, TimeSpan.Zero));
    }

    private async Task<int> CountRelationsAsync(Guid userId, Guid eventId)
    {
        var rows = await neo4j.QueryAsync(
            "MATCH (:User {UserId: $u})-[r:IS_GOING_TO]->(:Event {EventId: $e}) RETURN count(r) AS c",
            new { u = userId.ToString("D"), e = eventId.ToString("D") });
        return Convert.ToInt32(rows[0]["c"].As<long>());
    }

    private static AttendanceIntent Intent(Guid eventId, Guid userId, TransportMode mode, DateTimeOffset? at = null) =>
        new(eventId, userId, mode, at ?? Now);

    [Neo4jFact]
    public async Task UpsertAsync_FirstWrite_StoresSnapshotAndReturnsAggregatesFromSameTransaction()
    {
        var eventId = await CreateEventAsync();
        var userId = await neo4j.CreateUserAsync(49.8155, 19.0340);

        var result = await CreateRepository().UpsertAsync(Intent(eventId, userId, TransportMode.Bike));

        result.Should().NotBeNull();
        result!.IsNew.Should().BeTrue();
        result.ParticipantsCount.Should().Be(1);
        result.ModalSplit.Should().Be(new ModalSplit(0, 0, 1, 0, 0));

        var rows = await neo4j.QueryAsync(
            """
            MATCH (:User {UserId: $u})-[r:IS_GOING_TO]->(:Event {EventId: $e})
            RETURN r.TransportMode AS Mode, r.OriginLatitude AS Lat, r.OriginLongitude AS Lon, r.UpdatedAt AS At,
                   keys(r) AS Keys
            """,
            new { u = userId.ToString("D"), e = eventId.ToString("D") });
        rows.Should().HaveCount(1);
        rows[0]["Mode"].As<string>().Should().Be("Bike");
        rows[0]["Lat"].As<double>().Should().Be(49.8155);
        rows[0]["Lon"].As<double>().Should().Be(19.0340);
        rows[0]["At"].Should().NotBeNull();
        rows[0]["Keys"].As<List<object>>().Select(key => key.ToString()).Should().BeEquivalentTo(
            "TransportMode", "OriginLatitude", "OriginLongitude", "UpdatedAt");
    }

    [Neo4jFact]
    public async Task UpsertAsync_Repeat_IsNotNewAndDoesNotDoubleCount()
    {
        var eventId = await CreateEventAsync();
        var userId = await neo4j.CreateUserAsync();
        var repository = CreateRepository();

        await repository.UpsertAsync(Intent(eventId, userId, TransportMode.Walking));
        var second = await repository.UpsertAsync(Intent(eventId, userId, TransportMode.Walking));

        second!.IsNew.Should().BeFalse();
        second.ParticipantsCount.Should().Be(1);
        (await CountRelationsAsync(userId, eventId)).Should().Be(1);
    }

    [Neo4jFact]
    public async Task UpsertAsync_ModeChange_UpdatesSnapshotAndModalSplit()
    {
        var eventId = await CreateEventAsync();
        var userId = await neo4j.CreateUserAsync();
        var otherUser = await neo4j.CreateUserAsync();
        var repository = CreateRepository();
        await repository.UpsertAsync(Intent(eventId, otherUser, TransportMode.Car));
        await repository.UpsertAsync(Intent(eventId, userId, TransportMode.Walking, Now));

        var changed = await repository.UpsertAsync(Intent(eventId, userId, TransportMode.PublicTransport, Now.AddMinutes(5)));

        changed!.IsNew.Should().BeFalse();
        changed.ParticipantsCount.Should().Be(2);
        changed.ModalSplit.Should().Be(new ModalSplit(1, 0, 0, 1, 0));
        var origin = await CreateLookup().FindAsync(eventId, userId);
        origin!.Mode.Should().Be(TransportMode.PublicTransport);
    }

    [Neo4jFact]
    public async Task UpsertAsync_ReturnsNullAndWritesNothingWhenUserOrEventMissing()
    {
        var eventId = await CreateEventAsync();
        var userId = await neo4j.CreateUserAsync();
        var repository = CreateRepository();

        var missingUser = await repository.UpsertAsync(Intent(eventId, Guid.NewGuid(), TransportMode.Car));
        var missingEvent = await repository.UpsertAsync(Intent(Guid.NewGuid(), userId, TransportMode.Car));

        missingUser.Should().BeNull();
        missingEvent.Should().BeNull();
        (await CountRelationsAsync(userId, eventId)).Should().Be(0);
    }

    [Neo4jFact]
    public async Task UpsertAsync_UserWithoutOriginCoordinates_ThrowsAndRollsBack()
    {
        var eventId = await CreateEventAsync();
        var userId = Guid.NewGuid();
        await neo4j.ExecuteAsync(
            "CREATE (:User {UserId: $id, Name: 'Bez punktu startu', TestRunId: 'noorigin'})",
            new { id = userId.ToString("D") });

        try
        {
            var act = () => CreateRepository().UpsertAsync(Intent(eventId, userId, TransportMode.Car));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{userId:D}*");
            (await CountRelationsAsync(userId, eventId)).Should().Be(0);
        }
        finally
        {
            await neo4j.ExecuteAsync("MATCH (u:User {UserId: $id}) DETACH DELETE u", new { id = userId.ToString("D") });
        }
    }

    [Neo4jFact]
    public async Task UpsertAsync_ParallelWritesOfSamePair_CreateExactlyOneRelation()
    {
        const int parallelWrites = 25;
        var eventId = await CreateEventAsync();
        var userId = await neo4j.CreateUserAsync();
        var repository = CreateRepository();

        var results = await Task.WhenAll(Enumerable.Range(0, parallelWrites)
            .Select(_ => Task.Run(() => repository.UpsertAsync(Intent(eventId, userId, TransportMode.Walking)))));

        (await CountRelationsAsync(userId, eventId)).Should().Be(1);
        results.Should().OnlyContain(result => result != null);
        results.Count(result => result!.IsNew).Should().Be(1, "tylko jeden zapis tworzy relacje");
        results.Should().OnlyContain(result => result!.ParticipantsCount == 1);
    }

    [Neo4jFact]
    public async Task UpsertAsync_ParallelWritesOfDifferentUsers_CountEveryParticipantOnce()
    {
        const int users = 20;
        var eventId = await CreateEventAsync();
        var userIds = new List<Guid>();
        for (var i = 0; i < users; i++)
        {
            userIds.Add(await neo4j.CreateUserAsync());
        }

        var repository = CreateRepository();
        var results = await Task.WhenAll(userIds.Select((id, index) => Task.Run(() =>
            repository.UpsertAsync(Intent(eventId, id, index % 2 == 0 ? TransportMode.Walking : TransportMode.Car)))));

        results.Should().OnlyContain(result => result!.IsNew);
        results.Should().OnlyContain(result => result!.ParticipantsCount >= 1 && result.ParticipantsCount <= users);
        results.Max(result => result!.ParticipantsCount).Should().Be(users);
        var final = await repository.UpsertAsync(Intent(eventId, userIds[0], TransportMode.Walking));
        final!.ParticipantsCount.Should().Be(users);
        final.ModalSplit.Should().Be(new ModalSplit(0, users / 2, 0, users / 2, 0));
    }

    [Neo4jFact]
    public async Task DeleteAsync_RemovesRelationAndIsIdempotent()
    {
        var eventId = await CreateEventAsync();
        var userId = await neo4j.CreateUserAsync();
        var stays = await neo4j.CreateUserAsync();
        var repository = CreateRepository();
        await repository.UpsertAsync(Intent(eventId, stays, TransportMode.Bike));
        await repository.UpsertAsync(Intent(eventId, userId, TransportMode.Car));

        var first = await repository.DeleteAsync(eventId, userId);
        var second = await repository.DeleteAsync(eventId, userId);

        first.WasDeleted.Should().BeTrue();
        first.ParticipantsCount.Should().Be(1);
        first.ModalSplit.Should().Be(new ModalSplit(0, 0, 1, 0, 0));
        second.WasDeleted.Should().BeFalse();
        second.ParticipantsCount.Should().Be(1);
        (await CountRelationsAsync(userId, eventId)).Should().Be(0);
    }

    [Neo4jFact]
    public async Task DeleteAsync_ParallelDeletesOfSamePair_ReportExactlyOneDeletion()
    {
        var eventId = await CreateEventAsync();
        var userId = await neo4j.CreateUserAsync();
        var repository = CreateRepository();
        await repository.UpsertAsync(Intent(eventId, userId, TransportMode.Walking));

        var results = await Task.WhenAll(Enumerable.Range(0, 15)
            .Select(_ => Task.Run(() => repository.DeleteAsync(eventId, userId))));

        results.Count(result => result.WasDeleted).Should().Be(1);
        (await CountRelationsAsync(userId, eventId)).Should().Be(0);
    }

    [Neo4jFact]
    public async Task DeleteAsync_UnknownUserOrEvent_IsNotAnError()
    {
        var eventId = await CreateEventAsync();

        var unknownUser = await CreateRepository().DeleteAsync(eventId, Guid.NewGuid());
        var unknownEvent = await CreateRepository().DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        unknownUser.WasDeleted.Should().BeFalse();
        unknownUser.ParticipantsCount.Should().Be(0);
        unknownEvent.WasDeleted.Should().BeFalse();
        unknownEvent.ModalSplit.Should().Be(new ModalSplit(0, 0, 0, 0, 0));
    }

    [Neo4jFact]
    public async Task FindOriginAsync_ReturnsSnapshotAndNullWhenNotGoing()
    {
        var eventId = await CreateEventAsync();
        var userId = await neo4j.CreateUserAsync(49.8330, 19.0520);
        var lookup = CreateLookup();

        (await lookup.FindAsync(eventId, userId)).Should().BeNull();

        await CreateRepository().UpsertAsync(Intent(eventId, userId, TransportMode.Bike));
        var origin = await lookup.FindAsync(eventId, userId);

        origin.Should().NotBeNull();
        origin!.Mode.Should().Be(TransportMode.Bike);
        origin.Origin.Latitude.Should().Be(49.8330);
        origin.Origin.Longitude.Should().Be(19.0520);

        await CreateRepository().DeleteAsync(eventId, userId);
        (await lookup.FindAsync(eventId, userId)).Should().BeNull();
    }

    [Neo4jFact]
    public async Task FindOriginAsync_InvalidSnapshot_ThrowsWithoutLeakingCoordinates()
    {
        var eventId = await CreateEventAsync();
        var userId = await neo4j.CreateUserAsync();
        await neo4j.ExecuteAsync(
            """
            MATCH (u:User {UserId: $u}), (e:Event {EventId: $e})
            CREATE (u)-[:IS_GOING_TO {TransportMode: 'Car', OriginLatitude: 123.456, OriginLongitude: 19.0, UpdatedAt: datetime()}]->(e)
            """,
            new { u = userId.ToString("D"), e = eventId.ToString("D") });

        var act = () => CreateLookup().FindAsync(eventId, userId);

        var thrown = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        thrown.Message.Should().Contain(userId.ToString("D")).And.NotContain("123.456");
        thrown.InnerException.Should().BeNull();
    }
}
