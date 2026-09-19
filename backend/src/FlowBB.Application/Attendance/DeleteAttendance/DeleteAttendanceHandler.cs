using FlowBB.Application.Abstractions.Persistence;

namespace FlowBB.Application.Attendance.DeleteAttendance;

public sealed class DeleteAttendanceHandler
{
    private readonly IAttendanceRepository _repository;
    private readonly TimeProvider _timeProvider;

    public DeleteAttendanceHandler(
        IAttendanceRepository repository,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _repository = repository;
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

        return new DeleteAttendanceResult(
            command.EventId,
            command.UserId,
            persisted.WasDeleted,
            persisted.ParticipantsCount,
            persisted.ModalSplit,
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
