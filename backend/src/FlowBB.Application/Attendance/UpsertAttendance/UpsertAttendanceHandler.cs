using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Domain.Attendance;

namespace FlowBB.Application.Attendance.UpsertAttendance;

public sealed class UpsertAttendanceHandler
{
    private readonly IAttendanceRepository _repository;
    private readonly TimeProvider _timeProvider;

    public UpsertAttendanceHandler(
        IAttendanceRepository repository,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<UpsertAttendanceResult> HandleAsync(
        UpsertAttendanceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var attendance = new AttendanceIntent(
            command.EventId,
            command.UserId,
            command.TransportMode,
            _timeProvider.GetUtcNow());

        var persisted = await _repository.UpsertAsync(attendance, cancellationToken);

        return new UpsertAttendanceResult(
            attendance.EventId,
            attendance.UserId,
            attendance.TransportMode,
            persisted.ParticipantsCount,
            persisted.IsNew,
            attendance.UpdatedAt,
            persisted.ModalSplit);
    }
}
