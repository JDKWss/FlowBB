namespace FlowBB.Infrastructure.Neo4j;

public sealed partial class Neo4jFlowBbGraphRepository
{
    public Task SetUserGoingToEventAsync(string userId, string eventId)
    {
        const string query = """
            MATCH (u:User {UserId: $UserId})
            MATCH (e:Event {EventId: $EventId})
            MERGE (u)-[:IS_GOING_TO]->(e)
            RETURN count(*) AS Matches
            """;

        return ExecuteRelationshipAsync(
            query,
            new { UserId = userId, EventId = eventId },
            "Cannot create IS_GOING_TO because the user or event does not exist.");
    }

    public Task SetUserInterestedInEventAsync(string userId, string eventId)
    {
        const string query = """
            MATCH (u:User {UserId: $UserId})
            MATCH (e:Event {EventId: $EventId})
            MERGE (u)-[:IS_INTERESTED_IN]->(e)
            RETURN count(*) AS Matches
            """;

        return ExecuteRelationshipAsync(
            query,
            new { UserId = userId, EventId = eventId },
            "Cannot create IS_INTERESTED_IN because the user or event does not exist.");
    }

    public Task CreateFriendshipAsync(string firstUserId, string secondUserId)
    {
        if (string.Equals(firstUserId, secondUserId, StringComparison.Ordinal))
        {
            throw new ArgumentException("A user cannot be friends with themselves.", nameof(secondUserId));
        }

        const string query = """
            MATCH (first:User {UserId: $FirstUserId})
            MATCH (second:User {UserId: $SecondUserId})
            MERGE (first)-[:FRIENDS_WITH]->(second)
            MERGE (second)-[:FRIENDS_WITH]->(first)
            RETURN count(*) AS Matches
            """;

        return ExecuteRelationshipAsync(
            query,
            new { FirstUserId = firstUserId, SecondUserId = secondUserId },
            "Cannot create FRIENDS_WITH because one of the users does not exist.");
    }

    public Task FollowVenueAsync(string userId, string venueId)
    {
        const string query = """
            MATCH (u:User {UserId: $UserId})
            MATCH (v:Venue {VenueId: $VenueId})
            MERGE (u)-[:FOLLOWS]->(v)
            RETURN count(*) AS Matches
            """;

        return ExecuteRelationshipAsync(
            query,
            new { UserId = userId, VenueId = venueId },
            "Cannot create FOLLOWS because the user or venue does not exist.");
    }

    public Task HostEventAtVenueAsync(string eventId, string venueId)
    {
        const string query = """
            MATCH (e:Event {EventId: $EventId})
            MATCH (v:Venue {VenueId: $VenueId})
            MERGE (e)-[:HOSTED_AT]->(v)
            RETURN count(*) AS Matches
            """;

        return ExecuteRelationshipAsync(
            query,
            new { EventId = eventId, VenueId = venueId },
            "Cannot create HOSTED_AT because the event or venue does not exist.");
    }

    public Task AssignVenueManagerAsync(string ownerId, string venueId)
    {
        const string query = """
            MATCH (o:BusinessOwner {OwnerId: $OwnerId})
            MATCH (v:Venue {VenueId: $VenueId})
            MERGE (o)-[:MANAGES]->(v)
            RETURN count(*) AS Matches
            """;

        return ExecuteRelationshipAsync(
            query,
            new { OwnerId = ownerId, VenueId = venueId },
            "Cannot create MANAGES because the business owner or venue does not exist.");
    }

    public Task LikeTagAsync(string userId, string tagId)
    {
        const string query = """
            MATCH (u:User {UserId: $UserId})
            MATCH (t:Tag {TagId: $TagId})
            MERGE (u)-[:LIKES_TAG]->(t)
            RETURN count(*) AS Matches
            """;

        return ExecuteRelationshipAsync(
            query,
            new { UserId = userId, TagId = tagId },
            "Cannot create LIKES_TAG because the user or tag does not exist.");
    }

    public Task TagEventAsync(string eventId, string tagId)
    {
        const string query = """
            MATCH (e:Event {EventId: $EventId})
            MATCH (t:Tag {TagId: $TagId})
            MERGE (e)-[:HAS_TAG]->(t)
            RETURN count(*) AS Matches
            """;

        return ExecuteRelationshipAsync(
            query,
            new { EventId = eventId, TagId = tagId },
            "Cannot create HAS_TAG because the event or tag does not exist.");
    }

    public Task RemoveUserGoingToEventAsync(string userId, string eventId)
    {
        const string query = """
            MATCH (:User {UserId: $UserId})-[r:IS_GOING_TO]->(:Event {EventId: $EventId})
            DELETE r
            """;

        return ExecuteAsync(query, new { UserId = userId, EventId = eventId });
    }

    public Task RemoveUserInterestInEventAsync(string userId, string eventId)
    {
        const string query = """
            MATCH (:User {UserId: $UserId})-[r:IS_INTERESTED_IN]->(:Event {EventId: $EventId})
            DELETE r
            """;

        return ExecuteAsync(query, new { UserId = userId, EventId = eventId });
    }

    public Task RemoveFriendshipAsync(string firstUserId, string secondUserId)
    {
        const string query = """
            MATCH (:User {UserId: $FirstUserId})-[r:FRIENDS_WITH]-(:User {UserId: $SecondUserId})
            DELETE r
            """;

        return ExecuteAsync(
            query,
            new { FirstUserId = firstUserId, SecondUserId = secondUserId });
    }

    public Task UnfollowVenueAsync(string userId, string venueId)
    {
        const string query = """
            MATCH (:User {UserId: $UserId})-[r:FOLLOWS]->(:Venue {VenueId: $VenueId})
            DELETE r
            """;

        return ExecuteAsync(query, new { UserId = userId, VenueId = venueId });
    }

    public Task UnlikeTagAsync(string userId, string tagId)
    {
        const string query = """
            MATCH (:User {UserId: $UserId})-[r:LIKES_TAG]->(:Tag {TagId: $TagId})
            DELETE r
            """;

        return ExecuteAsync(query, new { UserId = userId, TagId = tagId });
    }
}
