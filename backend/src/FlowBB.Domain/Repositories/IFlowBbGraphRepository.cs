using FlowBB.Domain.Models;

namespace FlowBB.Domain.Repositories;

public interface IFlowBbGraphRepository
{
    Task VerifyConnectivityAsync();
    Task EnsureSchemaAsync();

    Task UpsertUserAsync(User user);
    Task UpsertEventAsync(Event @event);
    Task UpsertVenueAsync(Venue venue);
    Task UpsertBusinessOwnerAsync(BusinessOwner owner);
    Task UpsertTagAsync(Tag tag);
    Task UpsertCrewAsync(Crew crew);

    Task<User?> GetUserAsync(Guid userId);
    Task<Event?> GetEventAsync(Guid eventId);
    Task<Venue?> GetVenueAsync(string venueId);
    Task<BusinessOwner?> GetBusinessOwnerAsync(string ownerId);
    Task<Tag?> GetTagAsync(string tagId);
    Task<Crew?> GetCrewAsync(Guid crewId);

    Task SetUserGoingToEventAsync(Guid userId, Guid eventId);
    Task SetUserInterestedInEventAsync(Guid userId, Guid eventId);
    Task CreateFriendshipAsync(Guid firstUserId, Guid secondUserId);
    Task FollowVenueAsync(Guid userId, string venueId);
    Task HostEventAtVenueAsync(Guid eventId, string venueId);
    Task AssignVenueManagerAsync(string ownerId, string venueId);
    Task LikeTagAsync(Guid userId, string tagId);
    Task TagEventAsync(Guid eventId, string tagId);
    Task AssignCrewToEventAsync(Guid crewId, Guid eventId);
    Task AddUserToCrewAsync(Guid userId, Guid crewId);

    Task RemoveUserGoingToEventAsync(Guid userId, Guid eventId);
    Task RemoveUserInterestInEventAsync(Guid userId, Guid eventId);
    Task RemoveFriendshipAsync(Guid firstUserId, Guid secondUserId);
    Task UnfollowVenueAsync(Guid userId, string venueId);
    Task UnlikeTagAsync(Guid userId, string tagId);
    Task RemoveUserFromCrewAsync(Guid userId, Guid crewId);
}
