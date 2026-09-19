using System.Globalization;
using FlowBB.Domain.Models;
using Neo4j.Driver;
using DomainEvent = FlowBB.Domain.Models.Event;

namespace FlowBB.Infrastructure.Neo4j;

public sealed partial class Neo4jFlowBbGraphRepository
{
    public Task UpsertUserAsync(User user)
    {
        const string query = """
            MERGE (u:User {UserId: $UserId})
            SET u.Email = $Email,
                u.PasswordHash = $PasswordHash,
                u.Name = $Name
            """;

        return ExecuteAsync(query, user);
    }

    public Task UpsertEventAsync(DomainEvent @event)
    {
        const string query = """
            MERGE (e:Event {EventId: $EventId})
            SET e.Title = $Title,
                e.Description = $Description,
                e.EventUrl = $EventUrl,
                e.DateTime = $DateTime
            """;

        return ExecuteAsync(query, @event);
    }

    public Task UpsertVenueAsync(Venue venue)
    {
        const string query = """
            MERGE (v:Venue {VenueId: $VenueId})
            SET v.Name = $Name,
                v.Address = $Address,
                v.Latitude = $Latitude,
                v.Longitude = $Longitude
            """;

        return ExecuteAsync(query, venue);
    }

    public Task UpsertBusinessOwnerAsync(BusinessOwner owner)
    {
        const string query = """
            MERGE (o:BusinessOwner {OwnerId: $OwnerId})
            SET o.CompanyName = $CompanyName,
                o.Email = $Email,
                o.IsVerified = $IsVerified
            """;

        return ExecuteAsync(query, owner);
    }

    public Task UpsertTagAsync(Tag tag)
    {
        const string query = """
            MERGE (t:Tag {TagId: $TagId})
            SET t.Name = $Name
            """;

        return ExecuteAsync(query, tag);
    }

    public Task<User?> GetUserAsync(string userId)
    {
        const string query = """
            MATCH (u:User {UserId: $UserId})
            RETURN u.UserId AS UserId,
                   u.Email AS Email,
                   u.PasswordHash AS PasswordHash,
                   u.Name AS Name
            """;

        return ExecuteSingleAsync(query, new { UserId = userId }, MapUser);
    }

    public Task<DomainEvent?> GetEventAsync(string eventId)
    {
        const string query = """
            MATCH (e:Event {EventId: $EventId})
            RETURN e.EventId AS EventId,
                   e.Title AS Title,
                   e.Description AS Description,
                   e.EventUrl AS EventUrl,
                   toString(e.DateTime) AS DateTime
            """;

        return ExecuteSingleAsync(query, new { EventId = eventId }, MapEvent);
    }

    public Task<Venue?> GetVenueAsync(string venueId)
    {
        const string query = """
            MATCH (v:Venue {VenueId: $VenueId})
            RETURN v.VenueId AS VenueId,
                   v.Name AS Name,
                   v.Address AS Address,
                   v.Latitude AS Latitude,
                   v.Longitude AS Longitude
            """;

        return ExecuteSingleAsync(query, new { VenueId = venueId }, MapVenue);
    }

    public Task<BusinessOwner?> GetBusinessOwnerAsync(string ownerId)
    {
        const string query = """
            MATCH (o:BusinessOwner {OwnerId: $OwnerId})
            RETURN o.OwnerId AS OwnerId,
                   o.CompanyName AS CompanyName,
                   o.Email AS Email,
                   o.IsVerified AS IsVerified
            """;

        return ExecuteSingleAsync(query, new { OwnerId = ownerId }, MapBusinessOwner);
    }

    public Task<Tag?> GetTagAsync(string tagId)
    {
        const string query = """
            MATCH (t:Tag {TagId: $TagId})
            RETURN t.TagId AS TagId,
                   t.Name AS Name
            """;

        return ExecuteSingleAsync(query, new { TagId = tagId }, MapTag);
    }

    private static User MapUser(IRecord record)
    {
        return new User(
            record.Get<string>("UserId"),
            record.Get<string>("Email"),
            record.Get<string>("PasswordHash"),
            record.Get<string>("Name"));
    }

    private static DomainEvent MapEvent(IRecord record)
    {
        var dateTime = DateTimeOffset.Parse(
            record.Get<string>("DateTime"),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

        return new DomainEvent(
            record.Get<string>("EventId"),
            record.Get<string>("Title"),
            record.Get<string>("Description"),
            record.Get<string>("EventUrl"),
            dateTime);
    }

    private static Venue MapVenue(IRecord record)
    {
        return new Venue(
            record.Get<string>("VenueId"),
            record.Get<string>("Name"),
            record.Get<string>("Address"),
            record.Get<double>("Latitude"),
            record.Get<double>("Longitude"));
    }

    private static BusinessOwner MapBusinessOwner(IRecord record)
    {
        return new BusinessOwner(
            record.Get<string>("OwnerId"),
            record.Get<string>("CompanyName"),
            record.Get<string>("Email"),
            record.Get<bool>("IsVerified"));
    }

    private static Tag MapTag(IRecord record)
    {
        return new Tag(
            record.Get<string>("TagId"),
            record.Get<string>("Name"));
    }
}
