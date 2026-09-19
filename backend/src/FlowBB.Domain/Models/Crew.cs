namespace FlowBB.Domain.Models;

public sealed record Crew(
    Guid CrewId,
    string Name,
    string Description,
    int MaxMembers,
    IReadOnlyList<string> Tags,
    string MeetingPointName,
    double MeetingPointLatitude,
    double MeetingPointLongitude);
