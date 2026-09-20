using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Pulse;
using FlowBB.Domain.Attendance;

namespace FlowBB.Application.Attendance.UpsertAttendance;

public sealed class UpsertAttendanceHandler
{
    private readonly IAttendanceRepository _repository;
    private readonly IPulseDataReader _pulseReader;
    private readonly TimeProvider _timeProvider;

    public UpsertAttendanceHandler(
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

    /// <returns>Wynik zapisu albo <c>null</c>, gdy wydarzenie lub uzytkownik nie istnieje.</returns>
    public async Task<UpsertAttendanceResult?> HandleAsync(
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
        if (persisted is null)
        {
            return null;
        }

        // EndAt wydarzenia nie zalezy od deklaracji, wiec odczyt poza transakcja zapisu nie psuje spojnosci liczb.
        var info = await _pulseReader.GetEventAsync(attendance.EventId, cancellationToken);
        var withoutReturn = DemoReturnGapPolicy.CountParticipantsWithoutReturn(info?.EndAt, persisted.ModalSplit);

        return new UpsertAttendanceResult(
            attendance.EventId,
            attendance.UserId,
            attendance.TransportMode,
            persisted.ParticipantsCount,
            persisted.IsNew,
            attendance.UpdatedAt,
            persisted.ModalSplit,
            withoutReturn);
    }
}
