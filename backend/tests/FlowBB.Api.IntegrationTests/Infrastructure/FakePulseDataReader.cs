using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Pulse;

namespace FlowBB.Api.IntegrationTests.Infrastructure;

/// <summary>Fake portu odczytu PULSE dla testow integracyjnych API (bez prawdziwego Neo4j).</summary>
public sealed class FakePulseDataReader : IPulseDataReader
{
    private readonly Dictionary<Guid, (string Name, List<PulsePoint> Points, DateTimeOffset? EndAt)> _events = [];

    public FakePulseDataReader AddEvent(Guid id, string name, IEnumerable<PulsePoint> points, DateTimeOffset? endAt = null)
    {
        _events[id] = (name, points.ToList(), endAt);
        return this;
    }

    public Task<IReadOnlyList<PulsePoint>> GetPointsAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PulsePoint>>(_events.TryGetValue(eventId, out var e) ? e.Points : []);

    public Task<PulseEventInfo?> GetEventAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_events.TryGetValue(eventId, out var e) ? new PulseEventInfo(eventId, e.Name, e.EndAt) : null);

    public Task<IReadOnlyList<PulseEventInfo>> GetEventsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PulseEventInfo>>(_events.Select(e => new PulseEventInfo(e.Key, e.Value.Name, e.Value.EndAt)).ToList());

    public Task<IReadOnlyList<PulseEventSnapshot>> GetEventsWithPointsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PulseEventSnapshot>>(_events.Select(e =>
            new PulseEventSnapshot(new PulseEventInfo(e.Key, e.Value.Name, e.Value.EndAt), e.Value.Points)).ToList());
}
