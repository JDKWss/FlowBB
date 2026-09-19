using FlowBB.Domain.Common;

namespace FlowBB.Application.Attendance.UpsertAttendance;

public sealed record UpsertAttendanceCommand(
    Guid EventId,
    Guid UserId,
    TransportMode TransportMode);
