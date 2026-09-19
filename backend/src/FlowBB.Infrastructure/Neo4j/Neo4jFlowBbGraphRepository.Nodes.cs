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
                u.Name = $Name,
                u.DefaultOriginLatitude = $DefaultOriginLatitude,
                u.DefaultOriginLongitude = $DefaultOriginLongitude
            REMOVE u.HomeLatitude, u.HomeLongitude, u.DemoData
            """;

        return ExecuteAsync(query, new
        {
            UserId = ToDatabaseId(user.UserId),
            user.Email,
            user.PasswordHash,
            user.Name,
            user.DefaultOriginLatitude,
            user.DefaultOriginLongitude
        });
    }

    public Task UpsertEventAsync(DomainEvent @event)
    {
        const string query = """
            MERGE (e:Event {EventId: $EventId})
            SET e.Name = $Name,
                e.Description = $Description,
                e.EventUrl = $EventUrl,
                e.StartAt = $StartAt,
                e.EndAt = $EndAt
            REMOVE e.Title, e.DateTime, e.Source, e.Category
            """;

        return ExecuteAsync(query, new
        {
            EventId = ToDatabaseId(@event.EventId),
            @event.Name,
            @event.Description,
            @event.EventUrl,
            @event.StartAt,
            @event.EndAt
        });
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

    public Task UpsertCrewAsync(Crew crew)
    {
        const string query = """
            MERGE (c:Crew {CrewId: $CrewId})
            SET c.Name = $Name,
                c.Description = $Description,
                c.MaxMembers = $MaxMembers,
                c.Tags = $Tags,
                c.MeetingPointName = $MeetingPointName,
                c.MeetingPointLatitude = $MeetingPointLatitude,
                c.MeetingPointLongitude = $MeetingPointLongitude
            REMOVE c.DemoData
            """;

        return ExecuteAsync(query, new
        {
            CrewId = ToDatabaseId(crew.CrewId),
            crew.Name,
            crew.Description,
            crew.MaxMembers,
            Tags = crew.Tags.ToArray(),
            crew.MeetingPointName,
            crew.MeetingPointLatitude,
            crew.MeetingPointLongitude
        });
    }

    public Task<User?> GetUserAsync(Guid userId)
    {
        const string query = """
            MATCH (u:User {UserId: $UserId})
            RETURN u.UserId AS UserId,
                   u.Email AS Email,
                   u.PasswordHash AS PasswordHash,
                   u.Name AS Name,
                   u.DefaultOriginLatitude AS DefaultOriginLatitude,
                   u.DefaultOriginLongitude AS DefaultOriginLongitude
            """;

        return ExecuteSingleAsync(query, new { UserId = ToDatabaseId(userId) }, MapUser);
    }

    public Task<DomainEvent?> GetEventAsync(Guid eventId)
    {
        const string query = """
            MATCH (e:Event {EventId: $EventId})
            RETURN e.EventId AS EventId,
                   e.Name AS Name,
                   e.Description AS Description,
                   e.EventUrl AS EventUrl,
                   toString(e.StartAt) AS StartAt,
                   toString(e.EndAt) AS EndAt
            """;

        return ExecuteSingleAsync(query, new { EventId = ToDatabaseId(eventId) }, MapEvent);
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

    public Task<Crew?> GetCrewAsync(Guid crewId)
    {
        const string query = """
            MATCH (c:Crew {CrewId: $CrewId})
            RETURN c.CrewId AS CrewId,
                   c.Name AS Name,
                   c.Description AS Description,
                   c.MaxMembers AS MaxMembers,
                   c.Tags AS Tags,
                   c.MeetingPointName AS MeetingPointName,
                   c.MeetingPointLatitude AS MeetingPointLatitude,
                   c.MeetingPointLongitude AS MeetingPointLongitude
            """;

        return ExecuteSingleAsync(
            query,
            new { CrewId = ToDatabaseId(crewId) },
            MapCrew);
    }

    private static User MapUser(IRecord record)
    {
        return new User(
            FromDatabaseId(record.Get<string>("UserId")),
            record.Get<string>("Email"),
            record.Get<string>("PasswordHash"),
            record.Get<string>("Name"),
            record.Get<double>("DefaultOriginLatitude"),
            record.Get<double>("DefaultOriginLongitude"));
    }

    private static DomainEvent MapEvent(IRecord record)
    {
        var startAt = DateTimeOffset.Parse(
            record.Get<string>("StartAt"),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
        var endAtValue = record.Get<string?>("EndAt");
        DateTimeOffset? endAt = endAtValue is null
            ? null
            : DateTimeOffset.Parse(
                endAtValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);

        return new DomainEvent(
            FromDatabaseId(record.Get<string>("EventId")),
            record.Get<string>("Name"),
            record.Get<string>("Description"),
            record.Get<string>("EventUrl"),
            startAt,
            endAt);
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

    private static Crew MapCrew(IRecord record)
    {
        return new Crew(
            FromDatabaseId(record.Get<string>("CrewId")),
            record.Get<string>("Name"),
            record.Get<string>("Description"),
            record.Get<int>("MaxMembers"),
            record["Tags"].As<List<string>>(),
            record.Get<string>("MeetingPointName"),
            record.Get<double>("MeetingPointLatitude"),
            record.Get<double>("MeetingPointLongitude"));
    }
}
