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

    Task<IReadOnlyList<EventRecommendation>> GetRecommendedEventsAsync(Guid userId, int limit = 5);
    Task<IReadOnlyList<CrewRecommendation>> GetRecommendedCrewsAsync(Guid userId, int limit = 5);
    Task<IReadOnlyList<PersonRecommendation>> GetPeopleYouMayKnowAsync(Guid userId, int limit = 10);
    Task<IReadOnlyList<Tag>> GetEventTagsAsync(Guid eventId);
    Task<bool> SetDefaultOriginAsync(Guid userId, double latitude, double longitude);

    /// <summary>Login is the user's email. Does not issue tokens or authorize requests.</summary>
    Task<bool> VerifyLoginAsync(string email, string password);
    /// <summary>Trusted service operation: caller must authorize the password change or reset.</summary>
    Task<bool> SetUserPasswordAsync(Guid userId, string password);

    /// <summary>Trusted administration operation. Never expose as unauthenticated self-enrollment.</summary>
    Task AddOrganizationMemberAsync(Guid userId, string ownerId);
    Task RemoveOrganizationMemberAsync(Guid userId, string ownerId);
    Task<bool> IsOrganizationMemberAsync(Guid userId, string ownerId);
    Task<IReadOnlyList<Venue>> GetManagedVenuesAsync(Guid userId);
    /// <summary>
    /// Actor must come from the authenticated principal. Returns false if actor is not a member
    /// or organization does not manage the venue. Existing EventId causes a constraint error.
    /// </summary>
    Task<bool> CreateEventForBusinessOwnerAsync(Guid actorUserId, string ownerId, string venueId, Event @event);
}
