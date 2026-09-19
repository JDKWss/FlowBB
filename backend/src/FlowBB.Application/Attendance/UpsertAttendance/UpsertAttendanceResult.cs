using FlowBB.Application.Pulse;
using FlowBB.Domain.Common;

namespace FlowBB.Application.Attendance.UpsertAttendance;

public sealed record UpsertAttendanceResult(
    Guid EventId,
    Guid UserId,
    TransportMode TransportMode,
    int ParticipantsCount,
    bool IsNew,
    DateTimeOffset UpdatedAt,
    ModalSplit ModalSplit);
