using FlowBB.Application.Pulse;
using FlowBB.Domain.Attendance;

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
    ModalSplit ModalSplit);

public sealed record AttendanceDeletePersistenceResult(
    bool WasDeleted,
    int ParticipantsCount,
    ModalSplit ModalSplit);
