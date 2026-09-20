using FlowBB.Domain.Events;

namespace FlowBB.Application.Abstractions.Persistence;

/// <summary>Port sprawdzenia istnienia wydarzenia i odczytu jego podstawowych danych (uzywa go m.in. Attendance).</summary>
public interface IEventLookup
{
    Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<Event?> FindByIdAsync(Guid eventId, CancellationToken cancellationToken = default);
}
