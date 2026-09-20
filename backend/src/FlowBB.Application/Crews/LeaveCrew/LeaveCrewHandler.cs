using FlowBB.Application.Abstractions.Persistence;

namespace FlowBB.Application.Crews.LeaveCrew;

public sealed class LeaveCrewHandler(ICrewRepository crews)
{
    public Task HandleAsync(Guid crewId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (crewId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("Crew id and user id cannot be empty GUIDs.");
        }

        return crews.LeaveAsync(crewId, userId, cancellationToken);
    }
}
