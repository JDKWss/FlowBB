using FlowBB.Domain.Models;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

public sealed partial class Neo4jFlowBbGraphRepository
{
    public Task<IReadOnlyList<EventRecommendation>> GetRecommendedEventsAsync(Guid userId, int limit = 5)
    {
        ValidateLimit(limit);
        const string query = """
            MATCH (me:User {UserId: $UserId})-[:FRIENDS_WITH]-(friend:User)-[:IS_GOING_TO]->(e:Event)
            WHERE friend <> me AND e.StartAt >= datetime($Now)
              AND NOT EXISTS { MATCH (me)-[:IS_GOING_TO]->(e) }
            WITH e, count(DISTINCT friend) AS FriendsGoingCount
            ORDER BY FriendsGoingCount DESC, e.StartAt, e.EventId
            LIMIT $Limit
            RETURN e.EventId AS EventId, e.Name AS Name, e.Description AS Description,
                   e.EventUrl AS EventUrl, toString(e.StartAt) AS StartAt,
                   toString(e.EndAt) AS EndAt, FriendsGoingCount
            """;
        return ExecuteListAsync(query, new
        {
            UserId = ToDatabaseId(userId), Limit = limit, Now = DateTimeOffset.UtcNow.ToString("O")
        }, record => new EventRecommendation(MapEvent(record), record.Get<long>("FriendsGoingCount")));
    }

    public Task<IReadOnlyList<CrewRecommendation>> GetRecommendedCrewsAsync(Guid userId, int limit = 5)
    {
        ValidateLimit(limit);
        const string query = """
            MATCH (me:User {UserId: $UserId})-[:FRIENDS_WITH]-(friend:User)-[:MEMBER_OF]->(c:Crew)
                  -[:FOR_EVENT]->(e:Event)
            WHERE friend <> me AND e.StartAt >= datetime($Now)
              AND NOT EXISTS { MATCH (me)-[:MEMBER_OF]->(c) }
            WITH c, e, count(DISTINCT friend) AS FriendsCount
            OPTIONAL MATCH (member:User)-[:MEMBER_OF]->(c)
            WITH c, e, FriendsCount, count(DISTINCT member) AS MembersCount
            WHERE MembersCount < c.MaxMembers
            ORDER BY FriendsCount DESC, e.StartAt, c.CrewId, e.EventId
            LIMIT $Limit
            RETURN c.CrewId AS CrewId, c.Name AS Name, c.Description AS Description,
                   c.MaxMembers AS MaxMembers, c.Tags AS Tags,
                   c.MeetingPointName AS MeetingPointName,
                   c.MeetingPointLatitude AS MeetingPointLatitude,
                   c.MeetingPointLongitude AS MeetingPointLongitude,
                   e.EventId AS EventId, FriendsCount, MembersCount
            """;
        return ExecuteListAsync(query, new
        {
            UserId = ToDatabaseId(userId), Limit = limit, Now = DateTimeOffset.UtcNow.ToString("O")
        }, record => new CrewRecommendation(MapCrew(record), FromDatabaseId(record.Get<string>("EventId")),
            record.Get<long>("FriendsCount"), record.Get<long>("MembersCount")));
    }

    public Task<IReadOnlyList<PersonRecommendation>> GetPeopleYouMayKnowAsync(Guid userId, int limit = 10)
    {
        ValidateLimit(limit);
        const string query = """
            MATCH (me:User {UserId: $UserId})-[:FRIENDS_WITH]-(mutual:User)-[:FRIENDS_WITH]-(candidate:User)
            WHERE candidate <> me AND mutual <> me AND candidate <> mutual
              AND NOT EXISTS { MATCH (me)-[:FRIENDS_WITH]-(candidate) }
            WITH candidate, count(DISTINCT mutual) AS MutualFriendsCount
            ORDER BY MutualFriendsCount DESC, candidate.Name, candidate.UserId
            LIMIT $Limit
            RETURN candidate.UserId AS UserId, candidate.Name AS Name, MutualFriendsCount
            """;
        return ExecuteListAsync(query, new { UserId = ToDatabaseId(userId), Limit = limit },
            record => new PersonRecommendation(FromDatabaseId(record.Get<string>("UserId")),
                record.Get<string>("Name"), record.Get<long>("MutualFriendsCount")));
    }

    public Task<IReadOnlyList<Tag>> GetEventTagsAsync(Guid eventId)
    {
        const string query = """
            MATCH (:Event {EventId: $EventId})-[:HAS_TAG]->(t:Tag)
            RETURN DISTINCT t.TagId AS TagId, t.Name AS Name ORDER BY Name, TagId
            """;
        return ExecuteListAsync(query, new { EventId = ToDatabaseId(eventId) }, MapTag);
    }

    public Task<bool> SetDefaultOriginAsync(Guid userId, double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude));
        if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude));
        const string query = """
            MATCH (u:User {UserId: $UserId})
            SET u.DefaultOriginLatitude = $Latitude, u.DefaultOriginLongitude = $Longitude
            RETURN count(u) = 1 AS Success
            """;
        return ExecuteBooleanAsync(query, new
        {
            UserId = ToDatabaseId(userId), Latitude = latitude, Longitude = longitude
        });
    }
}
