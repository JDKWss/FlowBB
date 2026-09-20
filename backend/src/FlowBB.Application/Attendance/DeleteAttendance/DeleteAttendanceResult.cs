using FlowBB.Application.Pulse;

namespace FlowBB.Application.Attendance.DeleteAttendance;

public sealed record DeleteAttendanceResult(
    Guid EventId,
    Guid UserId,
    bool WasDeleted,
    int ParticipantsCount,
    ModalSplit ModalSplit,
    int ParticipantsWithoutReturn,
    DateTimeOffset ChangedAt);
