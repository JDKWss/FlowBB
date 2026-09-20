using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Pulse;

namespace FlowBB.Application.Attendance.DeleteAttendance;

public sealed class DeleteAttendanceHandler
{
    private readonly IAttendanceRepository _repository;
    private readonly IPulseDataReader _pulseReader;
    private readonly TimeProvider _timeProvider;

    public DeleteAttendanceHandler(
        IAttendanceRepository repository,
        IPulseDataReader pulseReader,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(pulseReader);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _repository = repository;
        _pulseReader = pulseReader;
        _timeProvider = timeProvider;
    }

    public async Task<DeleteAttendanceResult> HandleAsync(
        DeleteAttendanceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateIdentifiers(command);

        var persisted = await _repository.DeleteAsync(
            command.EventId,
            command.UserId,
            cancellationToken);

        var info = await _pulseReader.GetEventAsync(command.EventId, cancellationToken);
        var withoutReturn = DemoReturnGapPolicy.CountParticipantsWithoutReturn(info?.EndAt, persisted.ModalSplit);

        return new DeleteAttendanceResult(
            command.EventId,
            command.UserId,
            persisted.WasDeleted,
            persisted.ParticipantsCount,
            persisted.ModalSplit,
            withoutReturn,
            _timeProvider.GetUtcNow());
    }

    private static void ValidateIdentifiers(DeleteAttendanceCommand command)
    {
        if (command.EventId == Guid.Empty)
        {
            throw new ArgumentException("Event id cannot be empty.", nameof(command));
        }

        if (command.UserId == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(command));
        }
    }
}
