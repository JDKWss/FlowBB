using FlowBB.Application.Crews;
using FlowBB.Infrastructure.Neo4j;
using FluentAssertions;

namespace FlowBB.Infrastructure.Tests.Neo4j;

[Collection(Neo4jCollection.Name)]
public sealed class Neo4jCrewRepositoryTests(Neo4jFixture neo4j)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);

    private Neo4jCrewRepository CreateRepository() => new(neo4j.Driver!, neo4j.Options!);

    private async Task<Guid> CreateEventAsync()
    {
        var venueId = await neo4j.CreateVenueAsync();
        return await neo4j.CreateEventAsync(venueId, new DateTimeOffset(2100, 1, 1, 8, 0, 0, TimeSpan.Zero));
    }

    private async Task<int> CountMembershipsAsync(Guid userId, Guid crewId)
    {
        var rows = await neo4j.QueryAsync(
            "MATCH (:User {UserId: $u})-[m:MEMBER_OF]->(:Crew {CrewId: $c}) RETURN count(m) AS n",
            new { u = userId.ToString("D"), c = crewId.ToString("D") });
        return Convert.ToInt32(rows[0]["n"].As<long>());
    }

    private async Task<object?> StoredJoinedAtAsync(Guid userId, Guid crewId)
    {
        var rows = await neo4j.QueryAsync(
            "MATCH (:User {UserId: $u})-[m:MEMBER_OF]->(:Crew {CrewId: $c}) RETURN m.JoinedAt AS at",
            new { u = userId.ToString("D"), c = crewId.ToString("D") });
        return rows.Single()["at"];
    }

    [Neo4jFact]
    public async Task ListByEventAsync_ReturnsSummariesOrderedByNameWithJoinedFlag()
    {
        var eventId = await CreateEventAsync();
        var otherEvent = await CreateEventAsync();
        var second = await neo4j.CreateCrewAsync(eventId, 8, "B Crew");
        var first = await neo4j.CreateCrewAsync(eventId, 5, "A Crew");
        await neo4j.CreateCrewAsync(otherEvent, 5, "Inne wydarzenie");
        var member = await neo4j.CreateUserAsync();
        await neo4j.AddMemberAsync(member, first, Now);
        await neo4j.AddMemberAsync(await neo4j.CreateUserAsync(), first, Now);

        var forMember = await CreateRepository().ListByEventAsync(eventId, member);
        var anonymous = await CreateRepository().ListByEventAsync(eventId, null);

        forMember.Select(crew => crew.Id).Should().Equal(first, second);
        forMember[0].Should().Match<CrewSummary>(crew =>
            crew.EventId == eventId && crew.Name == "A Crew" && crew.Description == "Opis grupy" &&
            crew.CurrentMembers == 2 && crew.MaxMembers == 5 && crew.JoinedByCurrentUser);
        forMember[0].Tags.Should().Equal("muzyka", "centrum");
        forMember[0].MeetingPoint.Name.Should().Be("Fontanna");
        forMember[0].MeetingPoint.Latitude.Should().Be(49.82245);
        forMember[1].CurrentMembers.Should().Be(0);
        forMember[1].JoinedByCurrentUser.Should().BeFalse();
        anonymous.Should().OnlyContain(crew => !crew.JoinedByCurrentUser);
    }

    [Neo4jFact]
    public async Task ListByEventAsync_ReturnsEmptyListForEventWithoutCrews()
    {
        var eventId = await CreateEventAsync();

        var list = await CreateRepository().ListByEventAsync(eventId, null);

        list.Should().BeEmpty();
    }

    [Neo4jFact]
    public async Task TryJoinAsync_NewMember_JoinsAndReturnsStateAfterOperation()
    {
        var eventId = await CreateEventAsync();
        var crewId = await neo4j.CreateCrewAsync(eventId);
        var userId = await neo4j.CreateUserAsync();

        var result = await CreateRepository().TryJoinAsync(crewId, userId, Now);

        result.Outcome.Should().Be(JoinCrewOutcome.Joined);
        result.Crew.Should().NotBeNull();
        result.Crew!.CurrentMembers.Should().Be(1);
        result.Crew.JoinedByCurrentUser.Should().BeTrue();
        (await CountMembershipsAsync(userId, crewId)).Should().Be(1);
        (await StoredJoinedAtAsync(userId, crewId)).Should().NotBeNull();
    }

    [Neo4jFact]
    public async Task TryJoinAsync_Repeat_IsAlreadyMemberAndKeepsCountAndJoinedAt()
    {
        var eventId = await CreateEventAsync();
        var crewId = await neo4j.CreateCrewAsync(eventId);
        var userId = await neo4j.CreateUserAsync();
        var repository = CreateRepository();
        await repository.TryJoinAsync(crewId, userId, Now);
        var joinedAtBefore = await StoredJoinedAtAsync(userId, crewId);

        var repeat = await repository.TryJoinAsync(crewId, userId, Now.AddHours(3));

        repeat.Outcome.Should().Be(JoinCrewOutcome.AlreadyMember);
        repeat.Crew!.CurrentMembers.Should().Be(1);
        (await StoredJoinedAtAsync(userId, crewId)).Should().Be(joinedAtBefore);
        (await CountMembershipsAsync(userId, crewId)).Should().Be(1);
    }

    [Neo4jFact]
    public async Task TryJoinAsync_FullCrew_IsRefusedAndNotChanged()
    {
        var eventId = await CreateEventAsync();
        var crewId = await neo4j.CreateCrewAsync(eventId, maxMembers: 2);
        var repository = CreateRepository();
        var members = new[] { await neo4j.CreateUserAsync(), await neo4j.CreateUserAsync() };
        foreach (var member in members)
        {
            await repository.TryJoinAsync(crewId, member, Now);
        }

        var late = await neo4j.CreateUserAsync();
        var refused = await repository.TryJoinAsync(crewId, late, Now);
        var memberAgain = await repository.TryJoinAsync(crewId, members[0], Now);

        refused.Outcome.Should().Be(JoinCrewOutcome.Full);
        refused.Crew.Should().BeNull();
        (await CountMembershipsAsync(late, crewId)).Should().Be(0);
        memberAgain.Outcome.Should().Be(JoinCrewOutcome.AlreadyMember, "czlonek pelnej grupy nie jest odrzucany jako Full");
        memberAgain.Crew!.CurrentMembers.Should().Be(2);
    }

    [Neo4jFact]
    public async Task TryJoinAsync_MemberOfAnotherCrewOfSameEvent_IsRefusedButOtherEventIsAllowed()
    {
        var eventId = await CreateEventAsync();
        var otherEvent = await CreateEventAsync();
        var first = await neo4j.CreateCrewAsync(eventId);
        var second = await neo4j.CreateCrewAsync(eventId);
        var elsewhere = await neo4j.CreateCrewAsync(otherEvent);
        var userId = await neo4j.CreateUserAsync();
        var repository = CreateRepository();
        await repository.TryJoinAsync(first, userId, Now);

        var sameEvent = await repository.TryJoinAsync(second, userId, Now);
        var otherEventJoin = await repository.TryJoinAsync(elsewhere, userId, Now);

        sameEvent.Outcome.Should().Be(JoinCrewOutcome.InAnotherCrew);
        (await CountMembershipsAsync(userId, second)).Should().Be(0);
        otherEventJoin.Outcome.Should().Be(JoinCrewOutcome.Joined);
    }

    [Neo4jFact]
    public async Task TryJoinAsync_MissingCrewOrUser_ReturnsNotFoundOutcomes()
    {
        var eventId = await CreateEventAsync();
        var crewId = await neo4j.CreateCrewAsync(eventId);
        var userId = await neo4j.CreateUserAsync();
        var repository = CreateRepository();

        var noCrew = await repository.TryJoinAsync(Guid.NewGuid(), userId, Now);
        var noUser = await repository.TryJoinAsync(crewId, Guid.NewGuid(), Now);
        var neither = await repository.TryJoinAsync(Guid.NewGuid(), Guid.NewGuid(), Now);

        noCrew.Outcome.Should().Be(JoinCrewOutcome.CrewNotFound);
        noUser.Outcome.Should().Be(JoinCrewOutcome.UserNotFound);
        neither.Outcome.Should().Be(JoinCrewOutcome.CrewNotFound);
        new[] { noCrew, noUser, neither }.Should().OnlyContain(result => result.Crew == null);
    }

    [Neo4jFact]
    public async Task TryJoinAsync_ParallelJoinsNeverExceedMaxMembers()
    {
        const int maxMembers = 3;
        const int candidates = 12;
        var eventId = await CreateEventAsync();
        var crewId = await neo4j.CreateCrewAsync(eventId, maxMembers);
        var users = new List<Guid>();
        for (var i = 0; i < candidates; i++)
        {
            users.Add(await neo4j.CreateUserAsync());
        }

        var repository = CreateRepository();
        var results = await Task.WhenAll(users.Select(user => Task.Run(() => repository.TryJoinAsync(crewId, user, Now))));

        results.Count(result => result.Outcome == JoinCrewOutcome.Joined).Should().Be(maxMembers);
        results.Count(result => result.Outcome == JoinCrewOutcome.Full).Should().Be(candidates - maxMembers);
        var summary = (await repository.ListByEventAsync(eventId, null)).Single();
        summary.CurrentMembers.Should().Be(maxMembers);
    }

    [Neo4jFact]
    public async Task TryJoinAsync_ParallelJoinsOfOneUserToTwoCrewsOfSameEvent_SucceedOnlyOnce()
    {
        var eventId = await CreateEventAsync();
        var first = await neo4j.CreateCrewAsync(eventId);
        var second = await neo4j.CreateCrewAsync(eventId);
        var userId = await neo4j.CreateUserAsync();
        var repository = CreateRepository();

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(index =>
            Task.Run(() => repository.TryJoinAsync(index % 2 == 0 ? first : second, userId, Now))));

        results.Count(result => result.Outcome == JoinCrewOutcome.Joined).Should().Be(1);
        results.Should().OnlyContain(result =>
            result.Outcome == JoinCrewOutcome.Joined || result.Outcome == JoinCrewOutcome.AlreadyMember ||
            result.Outcome == JoinCrewOutcome.InAnotherCrew);
        var memberships = await CountMembershipsAsync(userId, first) + await CountMembershipsAsync(userId, second);
        memberships.Should().Be(1);
    }

    [Neo4jFact]
    public async Task TryJoinAsync_ParallelJoinsOfSameUserAndCrew_KeepFirstJoinedAt()
    {
        var eventId = await CreateEventAsync();
        var crewId = await neo4j.CreateCrewAsync(eventId);
        var userId = await neo4j.CreateUserAsync();
        var repository = CreateRepository();

        var results = await Task.WhenAll(Enumerable.Range(0, 15).Select(index =>
            Task.Run(() => repository.TryJoinAsync(crewId, userId, Now.AddMinutes(index)))));

        results.Count(result => result.Outcome == JoinCrewOutcome.Joined).Should().Be(1);
        results.Count(result => result.Outcome == JoinCrewOutcome.AlreadyMember).Should().Be(14);
        (await CountMembershipsAsync(userId, crewId)).Should().Be(1);
    }

    [Neo4jFact]
    public async Task LeaveAsync_RemovesMembershipIsIdempotentAndFreesSlot()
    {
        var eventId = await CreateEventAsync();
        var crewId = await neo4j.CreateCrewAsync(eventId, maxMembers: 2);
        var leaver = await neo4j.CreateUserAsync();
        var stays = await neo4j.CreateUserAsync();
        var repository = CreateRepository();
        await repository.TryJoinAsync(crewId, leaver, Now);
        await repository.TryJoinAsync(crewId, stays, Now);

        await repository.LeaveAsync(crewId, leaver);
        await repository.LeaveAsync(crewId, leaver);
        await repository.LeaveAsync(Guid.NewGuid(), Guid.NewGuid());

        (await CountMembershipsAsync(leaver, crewId)).Should().Be(0);
        (await CountMembershipsAsync(stays, crewId)).Should().Be(1);
        var replacement = await repository.TryJoinAsync(crewId, await neo4j.CreateUserAsync(), Now);
        replacement.Outcome.Should().Be(JoinCrewOutcome.Joined);
    }
}
