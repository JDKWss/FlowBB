using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Pulse;
using FlowBB.Domain.Attendance;
using FlowBB.Domain.Common;

namespace FlowBB.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Fake portu Attendance w pamieci. Odwzorowuje semantyke adaptera: jedna deklaracja na pare
/// (wydarzenie, uzytkownik), licznik wyliczany z deklaracji, <c>null</c> dla nieznanego wydarzenia lub uzytkownika.
/// </summary>
public sealed class FakeAttendanceRepository : IAttendanceRepository
{
    private readonly HashSet<Guid> _events = [];
    private readonly HashSet<Guid> _users = [];
    private readonly Dictionary<(Guid EventId, Guid UserId), TransportMode> _attendance = [];

    public Exception? FailWith { get; set; }

    public int Count(Guid eventId) => _attendance.Keys.Count(key => key.EventId == eventId);

    public FakeAttendanceRepository AddEvent(Guid eventId)
    {
        _events.Add(eventId);
        return this;
    }

    public FakeAttendanceRepository AddUser(Guid userId)
    {
        _users.Add(userId);
        return this;
    }

    public Task<AttendanceUpsertPersistenceResult?> UpsertAsync(
        AttendanceIntent attendance, CancellationToken cancellationToken = default)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        if (!_events.Contains(attendance.EventId) || !_users.Contains(attendance.UserId))
        {
            return Task.FromResult<AttendanceUpsertPersistenceResult?>(null);
        }

        var key = (attendance.EventId, attendance.UserId);
        var isNew = !_attendance.ContainsKey(key);
        _attendance[key] = attendance.TransportMode;

        return Task.FromResult<AttendanceUpsertPersistenceResult?>(
            new AttendanceUpsertPersistenceResult(isNew, Count(attendance.EventId), SplitFor(attendance.EventId)));
    }

    public Task<AttendanceDeletePersistenceResult> DeleteAsync(
        Guid eventId, Guid userId, CancellationToken cancellationToken = default)
    {
        var wasDeleted = _attendance.Remove((eventId, userId));
        return Task.FromResult(new AttendanceDeletePersistenceResult(wasDeleted, Count(eventId), SplitFor(eventId)));
    }

    private ModalSplit SplitFor(Guid eventId)
    {
        var modes = _attendance.Where(entry => entry.Key.EventId == eventId).Select(entry => entry.Value).ToList();
        return new ModalSplit(
            modes.Count(mode => mode == TransportMode.PublicTransport),
            modes.Count(mode => mode == TransportMode.Walking),
            modes.Count(mode => mode == TransportMode.Bike),
            modes.Count(mode => mode == TransportMode.Car),
            modes.Count(mode => mode == TransportMode.Unknown));
    }
}
