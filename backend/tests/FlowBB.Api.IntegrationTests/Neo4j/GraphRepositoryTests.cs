using FlowBB.Domain.Models;
using Neo4j.Driver;

namespace FlowBB.Api.IntegrationTests.Neo4j;

public sealed class GraphRepositoryTests
{
    [Neo4jFact]
    public async Task EventRecommendations_CountDistinctFriends_ExcludePastAndAlreadyJoined_LimitResults()
    {
        await using var data = new GraphTestData();
        var repo = data.Repository;
        var me = await data.UserAsync();
        var friend = await data.UserAsync();
        var otherFriend = await data.UserAsync();
        await repo.CreateFriendshipAsync(me.UserId, friend.UserId);
        await repo.CreateFriendshipAsync(me.UserId, otherFriend.UserId);
        var top = await data.EventAsync();
        await repo.SetUserGoingToEventAsync(friend.UserId, top.EventId);
        await repo.SetUserGoingToEventAsync(otherFriend.UserId, top.EventId);
        for (var i = 0; i < 6; i++)
        {
            var item = await data.EventAsync(31 + i);
            await repo.SetUserGoingToEventAsync(friend.UserId, item.EventId);
        }
        var past = await data.EventAsync(-1);
        var joined = await data.EventAsync();
        await repo.SetUserGoingToEventAsync(friend.UserId, past.EventId);
        await repo.SetUserGoingToEventAsync(friend.UserId, joined.EventId);
        await repo.SetUserGoingToEventAsync(me.UserId, joined.EventId);

        var results = await repo.GetRecommendedEventsAsync(me.UserId);
        Assert.Equal(5, results.Count);
        Assert.Equal(top.EventId, results[0].Event.EventId);
        Assert.Equal(2, results[0].FriendsGoingCount);
        Assert.All(results.Skip(1), r => Assert.Equal(1, r.FriendsGoingCount));
        Assert.DoesNotContain(results, r => r.Event.EventId == past.EventId || r.Event.EventId == joined.EventId);
        Assert.Single(await repo.GetRecommendedEventsAsync(me.UserId, 1));
        Assert.Empty(await repo.GetRecommendedEventsAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.GetRecommendedEventsAsync(me.UserId, 0));
    }

    [Neo4jFact]
    public async Task CrewRecommendations_ExcludeFullJoinedPast_AndCountDistinctFriends()
    {
        await using var data = new GraphTestData();
        var repo = data.Repository;
        var me = await data.UserAsync();
        var friend = await data.UserAsync();
        var second = await data.UserAsync();
        await repo.CreateFriendshipAsync(me.UserId, friend.UserId);
        await repo.CreateFriendshipAsync(me.UserId, second.UserId);
        var upcoming = await data.EventAsync();
        var past = await data.EventAsync(-1);
        var best = await data.CrewAsync(upcoming.EventId);
        var lower = await data.CrewAsync(upcoming.EventId);
        var full = await data.CrewAsync(upcoming.EventId, 2);
        var joined = await data.CrewAsync(upcoming.EventId);
        var expired = await data.CrewAsync(past.EventId);
        foreach (var crew in new[] { best, lower, full, joined, expired })
            await repo.AddUserToCrewAsync(friend.UserId, crew.CrewId);
        await repo.AddUserToCrewAsync(second.UserId, best.CrewId);
        await repo.AddUserToCrewAsync(second.UserId, full.CrewId);
        await repo.AddUserToCrewAsync(me.UserId, joined.CrewId);

        var results = await repo.GetRecommendedCrewsAsync(me.UserId);
        Assert.Equal(new[] { best.CrewId, lower.CrewId }, results.Select(r => r.Crew.CrewId));
        Assert.Equal(2, results[0].FriendsCount);
        Assert.Equal(2, results[0].MembersCount);
        Assert.Equal(upcoming.EventId, results[0].EventId);
        Assert.Single(await repo.GetRecommendedCrewsAsync(me.UserId, 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.GetRecommendedCrewsAsync(me.UserId, 101));
    }

    [Neo4jFact]
    public async Task PeopleYouMayKnow_ExcludeSelfAndFriends_CountDistinctMutualFriends()
    {
        await using var data = new GraphTestData();
        var repo = data.Repository;
        var me = await data.UserAsync();
        var a = await data.UserAsync();
        var b = await data.UserAsync();
        var candidate = await data.UserAsync();
        var lower = await data.UserAsync();
        await repo.CreateFriendshipAsync(me.UserId, a.UserId);
        await repo.CreateFriendshipAsync(me.UserId, b.UserId);
        await repo.CreateFriendshipAsync(a.UserId, b.UserId);
        await repo.CreateFriendshipAsync(a.UserId, candidate.UserId);
        await repo.CreateFriendshipAsync(b.UserId, candidate.UserId);
        await repo.CreateFriendshipAsync(a.UserId, lower.UserId);

        var results = await repo.GetPeopleYouMayKnowAsync(me.UserId);
        Assert.Equal(new[] { candidate.UserId, lower.UserId }, results.Select(r => r.UserId));
        Assert.Equal(2, results[0].MutualFriendsCount);
        Assert.Equal(1, results[1].MutualFriendsCount);
        Assert.Single(await repo.GetPeopleYouMayKnowAsync(me.UserId, 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.GetPeopleYouMayKnowAsync(me.UserId, 0));
    }

    [Neo4jFact]
    public async Task DefaultOrigin_UpdatesOnlyCoordinates_ValidatesRanges_HandlesMissingUser()
    {
        await using var data = new GraphTestData();
        var repo = data.Repository;
        var user = await data.UserAsync();
        Assert.True(await repo.SetDefaultOriginAsync(user.UserId, 49.82, 19.04));
        Assert.Equal(user with { DefaultOriginLatitude = 49.82, DefaultOriginLongitude = 19.04 },
            await repo.GetUserAsync(user.UserId));
        Assert.False(await repo.SetDefaultOriginAsync(Guid.NewGuid(), 0, 0));
        foreach (var latitude in new[] { double.NaN, double.PositiveInfinity, 91, -91 })
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.SetDefaultOriginAsync(user.UserId, latitude, 0));
        foreach (var longitude in new[] { double.NaN, double.NegativeInfinity, 181, -181 })
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.SetDefaultOriginAsync(user.UserId, 0, longitude));
    }

    [Neo4jFact]
    public async Task Login_UsesPasswordHash_RejectsBadPasswordMissingUserAndAmbiguousEmail()
    {
        await using var data = new GraphTestData();
        var repo = data.Repository;
        var user = await data.UserAsync();
        var password = Guid.NewGuid().ToString("N");
        Assert.False(await repo.VerifyLoginAsync(user.Email, password)); // Seed placeholder is not a hash.
        Assert.True(await repo.SetUserPasswordAsync(user.UserId, password));
        var stored = await repo.GetUserAsync(user.UserId);
        Assert.True(stored?.PasswordHash != password);
        Assert.True(await repo.VerifyLoginAsync(user.Email, password));
        Assert.True(await repo.VerifyLoginAsync($"  {user.Email.ToUpperInvariant()}  ", password));
        Assert.False(await repo.VerifyLoginAsync(user.Email, password + "x"));
        Assert.False(await repo.VerifyLoginAsync($"missing-{Guid.NewGuid():N}@example.invalid", password));
        Assert.False(await repo.VerifyLoginAsync("", password));
        Assert.False(await repo.VerifyLoginAsync(user.Email, ""));
        Assert.False(await repo.SetUserPasswordAsync(Guid.NewGuid(), password));
        await Assert.ThrowsAsync<ArgumentException>(() => repo.SetUserPasswordAsync(user.UserId, "short"));
        var duplicate = await data.UserAsync();
        await repo.UpsertUserAsync(duplicate with { Email = user.Email.ToUpperInvariant() });
        Assert.False(await repo.VerifyLoginAsync(user.Email, password));
    }

    [Neo4jFact]
    public async Task Organization_CanPublishOnlyAtManagedVenue_RecordsCreator_RejectsOverwrite()
    {
        await using var data = new GraphTestData();
        var repo = data.Repository;
        await repo.EnsureSchemaAsync();
        var actor = await data.UserAsync();
        var ownerId = data.NewId().ToString("D");
        var venueId = data.NewId().ToString("D");
        var otherVenue = data.NewId().ToString("D");
        await repo.UpsertBusinessOwnerAsync(new BusinessOwner(ownerId, "TEST Organization", $"{ownerId}@example.invalid", false));
        await repo.UpsertVenueAsync(new Venue(venueId, "TEST Venue", "Synthetic", 0, 0));
        await repo.UpsertVenueAsync(new Venue(otherVenue, "TEST Other Venue", "Synthetic", 0, 0));
        await repo.AssignVenueManagerAsync(ownerId, venueId);
        var item = new Event(data.NewId(), "TEST Publication", "Synthetic", "", DateTimeOffset.UtcNow.AddDays(30), null);
        Assert.False(await repo.CreateEventForBusinessOwnerAsync(actor.UserId, ownerId, venueId, item));
        Assert.Null(await repo.GetEventAsync(item.EventId));
        await repo.AddOrganizationMemberAsync(actor.UserId, ownerId);
        await repo.AddOrganizationMemberAsync(actor.UserId, ownerId);
        Assert.True(await repo.IsOrganizationMemberAsync(actor.UserId, ownerId));
        Assert.Equal(venueId, Assert.Single(await repo.GetManagedVenuesAsync(actor.UserId)).VenueId);
        Assert.False(await repo.CreateEventForBusinessOwnerAsync(actor.UserId, ownerId, otherVenue, item));
        Assert.True(await repo.CreateEventForBusinessOwnerAsync(actor.UserId, ownerId, venueId, item));
        Assert.Equal(item, await repo.GetEventAsync(item.EventId));
        await Assert.ThrowsAnyAsync<ClientException>(() => repo.CreateEventForBusinessOwnerAsync(
            actor.UserId, ownerId, venueId, item with { Name = "MUST NOT OVERWRITE" }));
        Assert.Equal(item.Name, (await repo.GetEventAsync(item.EventId))?.Name);
        var links = await data.QueryAsync("""
            MATCH (u:User {UserId: $UserId})-[m:IS_ORGANIZATION_MEMBER]->(o:BusinessOwner {OwnerId: $OwnerId})
                  -[:CREATED_EVENT]->(e:Event {EventId: $EventId})-[:HOSTED_AT]->(:Venue {VenueId: $VenueId})
            RETURN count(*) AS Count
            """, new { UserId = actor.UserId.ToString("D"), OwnerId = ownerId, EventId = item.EventId.ToString("D"), VenueId = venueId });
        Assert.Equal(1, links.Single().Get<long>("Count"));
        await repo.RemoveOrganizationMemberAsync(actor.UserId, ownerId);
        await repo.RemoveOrganizationMemberAsync(actor.UserId, ownerId);
        Assert.False(await repo.IsOrganizationMemberAsync(actor.UserId, ownerId));
        Assert.False(await repo.CreateEventForBusinessOwnerAsync(actor.UserId, ownerId, venueId, item with { EventId = data.NewId() }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.AddOrganizationMemberAsync(Guid.NewGuid(), ownerId));
    }

    [Neo4jFact]
    public async Task EventTags_AreUniqueSortedAndEmptyForMissingEvent()
    {
        await using var data = new GraphTestData();
        var repo = data.Repository;
        var item = await data.EventAsync();
        var a = new Tag(data.NewId().ToString("D"), "A");
        var b = new Tag(data.NewId().ToString("D"), "B");
        await repo.UpsertTagAsync(a);
        await repo.UpsertTagAsync(b);
        await repo.TagEventAsync(item.EventId, b.TagId);
        await repo.TagEventAsync(item.EventId, a.TagId);
        await repo.TagEventAsync(item.EventId, a.TagId);
        Assert.Equal(new[] { a, b }, await repo.GetEventTagsAsync(item.EventId));
        Assert.Empty(await repo.GetEventTagsAsync(Guid.NewGuid()));
    }
}
