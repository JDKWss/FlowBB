using FlowBB.Application.Abstractions.Persistence;

namespace FlowBB.Application.Crews.JoinCrew;

public sealed class JoinCrewHandler(ICrewRepository crews, TimeProvider timeProvider)
{
    public Task<CrewJoinResult> HandleAsync(Guid crewId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (crewId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("Crew id and user id cannot be empty GUIDs.");
        }

        return crews.TryJoinAsync(crewId, userId, timeProvider.GetUtcNow(), cancellationToken);
    }
}
