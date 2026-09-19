using FlowBB.Domain.Common;

namespace FlowBB.Application.Attendance.DeleteAttendance;

public sealed record DeleteAttendanceResult(
    Guid EventId,
    Guid UserId,
    bool WasDeleted,
    int ParticipantsCount,
    IReadOnlyDictionary<TransportMode, int> ModalSplit,
    DateTimeOffset ChangedAt);
