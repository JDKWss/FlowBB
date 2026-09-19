namespace FlowBB.Application.Attendance.DeleteAttendance;

public sealed record DeleteAttendanceCommand(Guid EventId, Guid UserId);
