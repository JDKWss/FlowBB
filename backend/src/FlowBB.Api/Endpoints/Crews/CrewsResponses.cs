using FlowBB.Application.Crews;

namespace FlowBB.Api.Endpoints.Crews;

// DTO odpowiadaja schematom GroupSummary i GroupMembershipRequest z contracts/openapi.yaml.
// Odpowiedz nie zawiera identyfikatorow czlonkow grupy.
public sealed record GroupMembershipRequest(string? UserId);

public sealed record MeetingPointResponse(string Name, double Latitude, double Longitude);

public sealed record GroupSummaryResponse(
    Guid Id,
    Guid EventId,
    string Name,
    string Description,
    int CurrentMembers,
    int MaxMembers,
    IReadOnlyList<string> Tags,
    MeetingPointResponse MeetingPoint,
    bool JoinedByCurrentUser);

public static class CrewResponseMapping
{
    public static GroupSummaryResponse ToResponse(this CrewSummary crew) =>
        new(
            crew.Id,
            crew.EventId,
            crew.Name,
            crew.Description,
            crew.CurrentMembers,
            crew.MaxMembers,
            crew.Tags,
            new MeetingPointResponse(crew.MeetingPoint.Name, crew.MeetingPoint.Latitude, crew.MeetingPoint.Longitude),
            crew.JoinedByCurrentUser);
}
