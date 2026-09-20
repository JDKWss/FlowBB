namespace FlowBB.Application.Crews;

public enum JoinCrewOutcome
{
    Joined,
    AlreadyMember,
    Full,
    InAnotherCrew,
    CrewNotFound,
    UserNotFound
}

/// <summary>Wynik proby dolaczenia. <c>Crew</c> jest stanem grupy po operacji (dla Joined i AlreadyMember).</summary>
public sealed record CrewJoinResult(JoinCrewOutcome Outcome, CrewSummary? Crew);
