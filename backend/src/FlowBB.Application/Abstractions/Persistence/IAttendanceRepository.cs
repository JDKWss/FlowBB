using FlowBB.Domain.Common;
using AttendanceIntent = FlowBB.Domain.Attendance.Attendance;

namespace FlowBB.Application.Abstractions.Persistence;

public interface IAttendanceRepository
{
    Task<AttendanceUpsertPersistenceResult> UpsertAsync(
        AttendanceIntent attendance,
        CancellationToken cancellationToken = default);

    Task<AttendanceDeletePersistenceResult> DeleteAsync(
        Guid eventId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record AttendanceUpsertPersistenceResult(
    bool IsNew,
    int ParticipantsCount,
    IReadOnlyDictionary<TransportMode, int> ModalSplit);

public sealed record AttendanceDeletePersistenceResult(
    bool WasDeleted,
    int ParticipantsCount,
    IReadOnlyDictionary<TransportMode, int> ModalSplit);
