using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Crews;
using FlowBB.Domain.Crews;

namespace FlowBB.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Stanowy fake portu Crew oparty o prawdziwa domene <see cref="Crew"/>, wiec limit czlonkow i idempotencja pochodza
/// z domeny. Regula "jedna grupa na wydarzenie" jest odtworzona tu tak, jak ma ja zapewnic adapter Neo4j.
/// </summary>
public sealed class FakeCrewRepository : ICrewRepository
{
    public static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid OtherEventId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid EmptyEventId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    public static readonly Guid NewcomersCrewId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid CyclistsCrewId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid TinyCrewId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid UserA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid UserB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid UserC = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid UnknownUser = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private readonly List<Crew> _crews;
    private readonly HashSet<Guid> _users = [UserA, UserB, UserC];
    private readonly Dictionary<(Guid CrewId, Guid UserId), DateTimeOffset> _joinedAt = [];

    public FakeCrewRepository()
    {
        var point = new MeetingPoint("Plac Chrobrego", 49.82205, 19.04318);
        _crews =
        [
            Crew.Create(NewcomersCrewId, EventId, "Nowi w Bielsku", "Dla nowych.", 6, ["muzyka"], point),
            Crew.Create(CyclistsCrewId, EventId, "Rowerzysci", "Dojazd rowerem.", 6, ["rower"], point),
            Crew.Create(TinyCrewId, OtherEventId, "Mala ekipa", "Dwie osoby.", 2, [], point)
        ];
    }

    public void Seed(Guid crewId, Guid userId) => _crews.Single(crew => crew.Id == crewId).Join(userId);

    public int CurrentMembers(Guid crewId) => _crews.Single(crew => crew.Id == crewId).CurrentMembers;

    public DateTimeOffset? JoinedAt(Guid crewId, Guid userId) =>
        _joinedAt.TryGetValue((crewId, userId), out var value) ? value : null;

    public Task<IReadOnlyList<CrewSummary>> ListByEventAsync(
        Guid eventId, Guid? userId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CrewSummary> result = _crews
            .Where(crew => crew.EventId == eventId)
            .Select(crew => ToSummary(crew, userId))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<CrewJoinResult> TryJoinAsync(
        Guid crewId, Guid userId, DateTimeOffset joinedAt, CancellationToken cancellationToken = default)
    {
        var crew = _crews.FirstOrDefault(item => item.Id == crewId);
        if (crew is null)
        {
            return Result(JoinCrewOutcome.CrewNotFound);
        }

        if (!_users.Contains(userId))
        {
            return Result(JoinCrewOutcome.UserNotFound);
        }

        if (crew.HasMember(userId))
        {
            return Result(JoinCrewOutcome.AlreadyMember, ToSummary(crew, userId));
        }

        if (_crews.Any(other => other.EventId == crew.EventId && other.HasMember(userId)))
        {
            return Result(JoinCrewOutcome.InAnotherCrew);
        }

        if (crew.Join(userId) == JoinCrewResult.Full)
        {
            return Result(JoinCrewOutcome.Full);
        }

        _joinedAt[(crewId, userId)] = joinedAt;
        return Result(JoinCrewOutcome.Joined, ToSummary(crew, userId));
    }

    public Task LeaveAsync(Guid crewId, Guid userId, CancellationToken cancellationToken = default)
    {
        _crews.FirstOrDefault(item => item.Id == crewId)?.Leave(userId);
        return Task.CompletedTask;
    }

    private static Task<CrewJoinResult> Result(JoinCrewOutcome outcome, CrewSummary? crew = null) =>
        Task.FromResult(new CrewJoinResult(outcome, crew));

    private static CrewSummary ToSummary(Crew crew, Guid? userId) => new(
        crew.Id,
        crew.EventId,
        crew.Name,
        crew.Description,
        crew.CurrentMembers,
        crew.MaxMembers,
        crew.Tags,
        crew.MeetingPoint,
        userId is not null && crew.HasMember(userId.Value));
}
