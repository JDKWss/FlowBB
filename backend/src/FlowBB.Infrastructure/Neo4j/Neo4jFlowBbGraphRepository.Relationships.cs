namespace FlowBB.Infrastructure.Neo4j;

public sealed partial class Neo4jFlowBbGraphRepository
{
    public Task SetUserGoingToEventAsync(Guid userId, Guid eventId)
    {
        const string query = """
            MATCH (u:User {UserId: $UserId})
            MATCH (e:Event {EventId: $EventId})
            MERGE (u)-[attendance:IS_GOING_TO]->(e)
            ON CREATE SET attendance.TransportMode = 'Unknown',
                          attendance.OriginLatitude = u.DefaultOriginLatitude,
                          attendance.OriginLongitude = u.DefaultOriginLongitude,
                          attendance.UpdatedAt = datetime()
            ON MATCH SET attendance.TransportMode = coalesce(attendance.TransportMode, 'Unknown'),
                         attendance.OriginLatitude = coalesce(attendance.OriginLatitude, u.DefaultOriginLatitude),
                         attendance.OriginLongitude = coalesce(attendance.OriginLongitude, u.DefaultOriginLongitude),
                         attendance.UpdatedAt = coalesce(attendance.UpdatedAt, datetime())
            RETURN count(*) AS Matches
            """;

        return ExecuteRelationshipAsync(
            query,
            new { UserId = ToDatabaseId(userId), EventId = ToDatabaseId(eventId) },
            "Cannot create IS_GOING_TO because the user or event does not exist.");
    }

    public Task SetUserInterestedInEventAsync(Guid userId, Guid eventId)
    {
        const string query = """
            MATCH (u:User {UserId: $UserId})
            MATCH (e:Event {EventId: $EventId})
            MERGE (u)-[:IS_INTERESTED_IN]->(e)
            RETURN count(*) AS Matches
            """;

        return ExecuteRelationshipAsync(
            query,
            new { UserId = ToDatabaseId(userId), EventId = ToDatabaseId(eventId) },
            "Cannot create IS_INTERESTED_IN because the user or event does not exist.");
    }

    public Task CreateFriendshipAsync(Guid firstUserId, Guid secondUserId)
    {
        if (firstUserId == secondUserId)
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
            new
            {
                FirstUserId = ToDatabaseId(firstUserId),
                SecondUserId = ToDatabaseId(secondUserId)
            },
            "Cannot create FRIENDS_WITH because one of the users does not exist.");
    }

    public Task FollowVenueAsync(Guid userId, string venueId)
    {
        const string query = """
            MATCH (u:User {UserId: $UserId})
            MATCH (v:Venue {VenueId: $VenueId})
            MERGE (u)-[:FOLLOWS]->(v)
            RETURN count(*) AS Matches
            """;

        return ExecuteRelationshipAsync(
            query,
            new { UserId = ToDatabaseId(userId), VenueId = venueId },
            "Cannot create FOLLOWS because the user or venue does not exist.");
    }

    public Task HostEventAtVenueAsync(Guid eventId, string venueId)
    {
        const string query = """
            MATCH (e:Event {EventId: $EventId})
            MATCH (v:Venue {VenueId: $VenueId})
            MERGE (e)-[:HOSTED_AT]->(v)
            RETURN count(*) AS Matches
            """;

        return ExecuteRelationshipAsync(
            query,
            new { EventId = ToDatabaseId(eventId), VenueId = venueId },
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

    public Task LikeTagAsync(Guid userId, string tagId)
    {
        const string query = """
            MATCH (u:User {UserId: $UserId})
            MATCH (t:Tag {TagId: $TagId})
            MERGE (u)-[:LIKES_TAG]->(t)
            RETURN count(*) AS Matches
            """;

        return ExecuteRelationshipAsync(
            query,
            new { UserId = ToDatabaseId(userId), TagId = tagId },
            "Cannot create LIKES_TAG because the user or tag does not exist.");
    }

    public Task TagEventAsync(Guid eventId, string tagId)
    {
        const string query = """
            MATCH (e:Event {EventId: $EventId})
            MATCH (t:Tag {TagId: $TagId})
            MERGE (e)-[:HAS_TAG]->(t)
            RETURN count(*) AS Matches
            """;

        return ExecuteRelationshipAsync(
            query,
            new { EventId = ToDatabaseId(eventId), TagId = tagId },
            "Cannot create HAS_TAG because the event or tag does not exist.");
    }

    public Task AssignCrewToEventAsync(Guid crewId, Guid eventId)
    {
        const string query = """
            MATCH (c:Crew {CrewId: $CrewId})
            MATCH (e:Event {EventId: $EventId})
            MERGE (c)-[:FOR_EVENT]->(e)
            RETURN count(*) AS Matches
            """;

        return ExecuteRelationshipAsync(
            query,
            new { CrewId = ToDatabaseId(crewId), EventId = ToDatabaseId(eventId) },
            "Cannot create FOR_EVENT because the crew or event does not exist.");
    }

    public Task AddUserToCrewAsync(Guid userId, Guid crewId)
    {
        const string query = """
            MATCH (u:User {UserId: $UserId})
            MATCH (c:Crew {CrewId: $CrewId})
            MERGE (u)-[:MEMBER_OF]->(c)
            RETURN count(*) AS Matches
            """;

        return ExecuteRelationshipAsync(
            query,
            new { UserId = ToDatabaseId(userId), CrewId = ToDatabaseId(crewId) },
            "Cannot create MEMBER_OF because the user or crew does not exist.");
    }

    public Task RemoveUserGoingToEventAsync(Guid userId, Guid eventId)
    {
        const string query = """
            MATCH (:User {UserId: $UserId})-[r:IS_GOING_TO]->(:Event {EventId: $EventId})
            DELETE r
            """;

        return ExecuteAsync(
            query,
            new { UserId = ToDatabaseId(userId), EventId = ToDatabaseId(eventId) });
    }

    public Task RemoveUserInterestInEventAsync(Guid userId, Guid eventId)
    {
        const string query = """
            MATCH (:User {UserId: $UserId})-[r:IS_INTERESTED_IN]->(:Event {EventId: $EventId})
            DELETE r
            """;

        return ExecuteAsync(
            query,
            new { UserId = ToDatabaseId(userId), EventId = ToDatabaseId(eventId) });
    }

    public Task RemoveFriendshipAsync(Guid firstUserId, Guid secondUserId)
    {
        const string query = """
            MATCH (:User {UserId: $FirstUserId})-[r:FRIENDS_WITH]-(:User {UserId: $SecondUserId})
            DELETE r
            """;

        return ExecuteAsync(
            query,
            new
            {
                FirstUserId = ToDatabaseId(firstUserId),
                SecondUserId = ToDatabaseId(secondUserId)
            });
    }

    public Task UnfollowVenueAsync(Guid userId, string venueId)
    {
        const string query = """
            MATCH (:User {UserId: $UserId})-[r:FOLLOWS]->(:Venue {VenueId: $VenueId})
            DELETE r
            """;

        return ExecuteAsync(query, new { UserId = ToDatabaseId(userId), VenueId = venueId });
    }

    public Task UnlikeTagAsync(Guid userId, string tagId)
    {
        const string query = """
            MATCH (:User {UserId: $UserId})-[r:LIKES_TAG]->(:Tag {TagId: $TagId})
            DELETE r
            """;

        return ExecuteAsync(query, new { UserId = ToDatabaseId(userId), TagId = tagId });
    }

    public Task RemoveUserFromCrewAsync(Guid userId, Guid crewId)
    {
        const string query = """
            MATCH (:User {UserId: $UserId})-[r:MEMBER_OF]->(:Crew {CrewId: $CrewId})
            DELETE r
            """;

        return ExecuteAsync(
            query,
            new { UserId = ToDatabaseId(userId), CrewId = ToDatabaseId(crewId) });
    }
}
