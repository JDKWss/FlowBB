using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Events;
using FlowBB.Domain.Common;
using FlowBB.Domain.Events;

namespace FlowBB.Api.IntegrationTests.Infrastructure;

/// <summary>Fake portu odczytu Events dla testow integracyjnych API (bez prawdziwego Neo4j).</summary>
public sealed class FakeEventRepository : IEventRepository
{
    public static readonly Guid ConcertId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid BoardGamesId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly List<EventWithParticipants> _events;

    public FakeEventRepository(IEnumerable<EventWithParticipants>? events = null)
    {
        _events = events?.ToList() ?? [];
    }

    public DateTimeOffset? LastFrom { get; private set; }

    public DateTimeOffset? LastTo { get; private set; }

    /// <summary>Dwa wydarzenia demo: z <c>EndAt</c> (czas w UTC, latem +02:00 w Warszawie) i bez.</summary>
    public static FakeEventRepository WithDemoEvents() => new(
    [
        new EventWithParticipants(
            new Event(
                ConcertId,
                "Koncert na Rynku",
                "Wieczorny koncert w centrum Bielska-Bialej.",
                new DateTimeOffset(2026, 9, 25, 17, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 25, 19, 30, 0, TimeSpan.Zero),
                "Rynek w Bielsku-Bialej",
                EventCategory.Culture,
                EventSource.Demo,
                new GeoPoint(49.82245, 19.04431)),
            82),
        new EventWithParticipants(
            new Event(
                BoardGamesId,
                "Wieczor z Planszowkami",
                string.Empty,
                new DateTimeOffset(2026, 10, 2, 16, 0, 0, TimeSpan.Zero),
                null,
                "Aquarium",
                EventCategory.Community,
                EventSource.City,
                new GeoPoint(49.8222, 19.0469)),
            0)
    ]);

    public Task<IReadOnlyList<EventWithParticipants>> ListAsync(
        DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default)
    {
        LastFrom = from;
        LastTo = to;

        IReadOnlyList<EventWithParticipants> result = _events
            .Where(item => (from is null || item.Event.StartAt >= from) && (to is null || item.Event.StartAt <= to))
            .OrderBy(item => item.Event.StartAt)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<EventWithParticipants?> FindAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_events.FirstOrDefault(item => item.Event.Id == eventId));
}
