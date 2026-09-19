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

    Task<User?> GetUserAsync(string userId);
    Task<Event?> GetEventAsync(string eventId);
    Task<Venue?> GetVenueAsync(string venueId);
    Task<BusinessOwner?> GetBusinessOwnerAsync(string ownerId);
    Task<Tag?> GetTagAsync(string tagId);

    Task SetUserGoingToEventAsync(string userId, string eventId);
    Task SetUserInterestedInEventAsync(string userId, string eventId);
    Task CreateFriendshipAsync(string firstUserId, string secondUserId);
    Task FollowVenueAsync(string userId, string venueId);
    Task HostEventAtVenueAsync(string eventId, string venueId);
    Task AssignVenueManagerAsync(string ownerId, string venueId);
    Task LikeTagAsync(string userId, string tagId);
    Task TagEventAsync(string eventId, string tagId);

    Task RemoveUserGoingToEventAsync(string userId, string eventId);
    Task RemoveUserInterestInEventAsync(string userId, string eventId);
    Task RemoveFriendshipAsync(string firstUserId, string secondUserId);
    Task UnfollowVenueAsync(string userId, string venueId);
    Task UnlikeTagAsync(string userId, string tagId);
}
