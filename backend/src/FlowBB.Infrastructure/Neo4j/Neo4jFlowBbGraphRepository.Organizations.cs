using FlowBB.Domain.Models;

namespace FlowBB.Infrastructure.Neo4j;

public sealed partial class Neo4jFlowBbGraphRepository
{
    public Task AddOrganizationMemberAsync(Guid userId, string ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        const string query = """
            MATCH (u:User {UserId: $UserId}), (o:BusinessOwner {OwnerId: $OwnerId})
            MERGE (u)-[:IS_ORGANIZATION_MEMBER]->(o)
            RETURN count(*) AS Matches
            """;
        return ExecuteRelationshipAsync(query, new { UserId = ToDatabaseId(userId), OwnerId = ownerId },
            "Cannot add organization member because the user or business owner does not exist.");
    }

    public Task RemoveOrganizationMemberAsync(Guid userId, string ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        const string query = """
            MATCH (:User {UserId: $UserId})-[r:IS_ORGANIZATION_MEMBER]->(:BusinessOwner {OwnerId: $OwnerId})
            DELETE r
            """;
        return ExecuteAsync(query, new { UserId = ToDatabaseId(userId), OwnerId = ownerId });
    }

    public Task<bool> IsOrganizationMemberAsync(Guid userId, string ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        const string query = """
            RETURN EXISTS {
                MATCH (:User {UserId: $UserId})-[:IS_ORGANIZATION_MEMBER]->(:BusinessOwner {OwnerId: $OwnerId})
            } AS Success
            """;
        return ExecuteBooleanAsync(query, new { UserId = ToDatabaseId(userId), OwnerId = ownerId });
    }

    public Task<IReadOnlyList<Venue>> GetManagedVenuesAsync(Guid userId)
    {
        const string query = """
            MATCH (:User {UserId: $UserId})-[:IS_ORGANIZATION_MEMBER]->(:BusinessOwner)-[:MANAGES]->(v:Venue)
            RETURN DISTINCT v.VenueId AS VenueId, v.Name AS Name, v.Address AS Address,
                   v.Latitude AS Latitude, v.Longitude AS Longitude
            ORDER BY Name, VenueId
            """;
        return ExecuteListAsync(query, new { UserId = ToDatabaseId(userId) }, MapVenue);
    }

    public Task<bool> CreateEventForBusinessOwnerAsync(
        Guid actorUserId, string ownerId, string venueId, Event @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(venueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(@event.Name);
        if (@event.EventId == Guid.Empty || @event.EndAt < @event.StartAt)
            throw new ArgumentException("Event requires a non-empty ID and EndAt >= StartAt.", nameof(@event));

        const string query = """
            MATCH (:User {UserId: $ActorId})-[:IS_ORGANIZATION_MEMBER]->(o:BusinessOwner {OwnerId: $OwnerId})
                  -[:MANAGES]->(v:Venue {VenueId: $VenueId})
            WITH DISTINCT o, v
            CREATE (e:Event {EventId: $EventId, Name: $Name, Description: $Description,
                            EventUrl: $EventUrl, StartAt: $StartAt, EndAt: $EndAt})
            CREATE (e)-[:HOSTED_AT]->(v)
            CREATE (o)-[:CREATED_EVENT]->(e)
            RETURN count(e) = 1 AS Success
            """;
        return ExecuteBooleanAsync(query, new
        {
            ActorId = ToDatabaseId(actorUserId), OwnerId = ownerId, VenueId = venueId,
            EventId = ToDatabaseId(@event.EventId), @event.Name, @event.Description, @event.EventUrl,
            StartAt = @event.StartAt.ToUniversalTime(), EndAt = @event.EndAt?.ToUniversalTime()
        });
    }
}
