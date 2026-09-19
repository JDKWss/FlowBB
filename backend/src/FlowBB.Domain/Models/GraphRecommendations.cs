namespace FlowBB.Domain.Models;

public sealed record EventRecommendation(Event Event, long FriendsGoingCount);

public sealed record CrewRecommendation(
    Crew Crew, Guid EventId, long FriendsCount, long MembersCount);

// Deliberately excludes credentials, email and the user's origin coordinates.
public sealed record PersonRecommendation(Guid UserId, string Name, long MutualFriendsCount);
