using FlowBB.Domain.Crews;

namespace FlowBB.Application.Crews;

/// <summary>
/// Publiczny widok mikrogrupy zgodny z <c>GroupSummary</c> z OpenAPI. Nie zawiera identyfikatorow czlonkow:
/// <see cref="JoinedByCurrentUser"/> jest wyliczane dla uzytkownika z zapytania.
/// </summary>
public sealed record CrewSummary(
    Guid Id,
    Guid EventId,
    string Name,
    string Description,
    int CurrentMembers,
    int MaxMembers,
    IReadOnlyList<string> Tags,
    MeetingPoint MeetingPoint,
    bool JoinedByCurrentUser);
