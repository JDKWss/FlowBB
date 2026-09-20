using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Crews;
using FlowBB.Domain.Crews;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

/// <summary>
/// Adapter <see cref="ICrewRepository"/>. Limit <c>MaxMembers</c> i regula "jedna grupa na wydarzenie" sa chronione
/// w persystencji: <see cref="TryJoinAsync"/> zaklada blokady zapisu na wezlach Crew i User (zawsze w tej kolejnosci),
/// a dopiero potem czyta stan i zapisuje <c>MEMBER_OF</c>, wszystko w jednej transakcji.
/// </summary>
public sealed class Neo4jCrewRepository(IDriver driver, Neo4jOptions options) : ICrewRepository
{
    // No-op SET zaklada blokade zapisu (trzymana do commitu). SET na wartosci null (brak wezla) jest pomijany.
    private const string LockQuery = """
        OPTIONAL MATCH (c:Crew {CrewId: $crewId})
        OPTIONAL MATCH (u:User {UserId: $userId})
        SET c.CrewId = c.CrewId, u.UserId = u.UserId
        RETURN c IS NOT NULL AS CrewExists, u IS NOT NULL AS UserExists
        """;

    private const string JoinStateQuery = """
        MATCH (c:Crew {CrewId: $crewId})
        MATCH (u:User {UserId: $userId})
        OPTIONAL MATCH (c)-[:FOR_EVENT]->(e:Event)
        RETURN c.MaxMembers AS MaxMembers,
               size([(:User)-[:MEMBER_OF]->(c) | 1]) AS Members,
               EXISTS { (u)-[:MEMBER_OF]->(c) } AS IsMember,
               EXISTS { (u)-[:MEMBER_OF]->(other:Crew)-[:FOR_EVENT]->(e) WHERE other <> c } AS InOtherCrew
        """;

    private const string AddMemberQuery = """
        MATCH (c:Crew {CrewId: $crewId})
        MATCH (u:User {UserId: $userId})
        MERGE (u)-[m:MEMBER_OF]->(c)
        ON CREATE SET m.JoinedAt = $joinedAt
        """;

    private const string SummaryProjection = """
        OPTIONAL MATCH (m:User)-[:MEMBER_OF]->(c)
        WITH c, e, count(m) AS CurrentMembers, collect(m.UserId) AS MemberIds
        RETURN c.CrewId AS CrewId, e.EventId AS EventId, c.Name AS Name, coalesce(c.Description, '') AS Description,
               CurrentMembers, c.MaxMembers AS MaxMembers, coalesce(c.Tags, []) AS Tags,
               c.MeetingPointName AS MeetingPointName, c.MeetingPointLatitude AS MeetingPointLatitude,
               c.MeetingPointLongitude AS MeetingPointLongitude,
               coalesce($userId IN MemberIds, false) AS Joined
        """;

    private static readonly string ListQuery =
        "MATCH (c:Crew)-[:FOR_EVENT]->(e:Event {EventId: $eventId})\n" + SummaryProjection +
        "\nORDER BY c.Name ASC, c.CrewId ASC";

    private static readonly string SummaryQuery =
        "MATCH (c:Crew {CrewId: $crewId})-[:FOR_EVENT]->(e:Event)\n" + SummaryProjection;

    private const string LeaveQuery = """
        MATCH (:User {UserId: $userId})-[m:MEMBER_OF]->(:Crew {CrewId: $crewId})
        DELETE m
        """;

    private readonly record struct JoinState(long? MaxMembers, long Members, bool IsMember, bool InOtherCrew);

    public async Task<IReadOnlyList<CrewSummary>> ListByEventAsync(
        Guid eventId,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["eventId"] = Neo4jValueConversions.ToDatabaseId(eventId),
            ["userId"] = userId is null ? null : Neo4jValueConversions.ToDatabaseId(userId.Value)
        };

        await using var session = OpenSession();
        var records = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(ListQuery, parameters);
            return await cursor.ToListAsync(cancellationToken);
        });

        return records.Select(MapSummary).ToList();
    }

    public async Task<CrewJoinResult> TryJoinAsync(
        Guid crewId,
        Guid userId,
        DateTimeOffset joinedAt,
        CancellationToken cancellationToken = default)
    {
        await using var session = OpenSession();
        return await session.ExecuteWriteAsync(tx => JoinInTransactionAsync(tx, crewId, userId, joinedAt, cancellationToken));
    }

    public async Task LeaveAsync(Guid crewId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var session = OpenSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(LeaveQuery, IdParameters(crewId, userId));
            await cursor.ConsumeAsync();
        });
    }

    private IAsyncSession OpenSession()
    {
        return driver.AsyncSession(config => config.WithDatabase(options.Database));
    }

    private static Dictionary<string, object?> IdParameters(Guid crewId, Guid userId)
    {
        return new Dictionary<string, object?>
        {
            ["crewId"] = Neo4jValueConversions.ToDatabaseId(crewId),
            ["userId"] = Neo4jValueConversions.ToDatabaseId(userId)
        };
    }

    private static async Task<CrewJoinResult> JoinInTransactionAsync(
        IAsyncQueryRunner tx,
        Guid crewId,
        Guid userId,
        DateTimeOffset joinedAt,
        CancellationToken cancellationToken)
    {
        var missing = await LockAsync(tx, crewId, userId, cancellationToken);
        if (missing is not null)
        {
            return new CrewJoinResult(missing.Value, null);
        }

        var outcome = Decide(await ReadJoinStateAsync(tx, crewId, userId, cancellationToken));
        if (outcome == JoinCrewOutcome.Joined)
        {
            var parameters = IdParameters(crewId, userId);
            parameters["joinedAt"] = joinedAt.ToUniversalTime();
            await (await tx.RunAsync(AddMemberQuery, parameters)).ConsumeAsync();
        }

        var succeeded = outcome is JoinCrewOutcome.Joined or JoinCrewOutcome.AlreadyMember;
        var summary = succeeded ? await ReadSummaryAsync(tx, crewId, userId, cancellationToken) : null;
        return new CrewJoinResult(outcome, summary);
    }

    /// <returns><c>CrewNotFound</c> lub <c>UserNotFound</c>, a <c>null</c>, gdy oba wezly istnieja i sa zablokowane.</returns>
    private static async Task<JoinCrewOutcome?> LockAsync(
        IAsyncQueryRunner tx,
        Guid crewId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var cursor = await tx.RunAsync(LockQuery, IdParameters(crewId, userId));
        var row = (await cursor.ToListAsync(cancellationToken))[0];

        if (!row["CrewExists"].As<bool>())
        {
            return JoinCrewOutcome.CrewNotFound;
        }

        return row["UserExists"].As<bool>() ? null : JoinCrewOutcome.UserNotFound;
    }

    private static async Task<JoinState> ReadJoinStateAsync(
        IAsyncQueryRunner tx,
        Guid crewId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var cursor = await tx.RunAsync(JoinStateQuery, IdParameters(crewId, userId));
        var row = (await cursor.ToListAsync(cancellationToken))[0];

        return new JoinState(
            row["MaxMembers"].As<long?>(),
            row["Members"].As<long>(),
            row["IsMember"].As<bool>(),
            row["InOtherCrew"].As<bool>());
    }

    /// <summary>Kolejnosc regul: juz czlonek, czlonek innej grupy tego wydarzenia, pelna grupa, dopisanie.</summary>
    private static JoinCrewOutcome Decide(JoinState state)
    {
        if (state.IsMember)
        {
            return JoinCrewOutcome.AlreadyMember;
        }

        if (state.InOtherCrew)
        {
            return JoinCrewOutcome.InAnotherCrew;
        }

        return state.MaxMembers is null || state.Members >= state.MaxMembers
            ? JoinCrewOutcome.Full
            : JoinCrewOutcome.Joined;
    }

    private static async Task<CrewSummary> ReadSummaryAsync(
        IAsyncQueryRunner tx,
        Guid crewId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var cursor = await tx.RunAsync(SummaryQuery, IdParameters(crewId, userId));
        var records = await cursor.ToListAsync(cancellationToken);

        return records.Count == 0
            ? throw new InvalidOperationException($"Crew {crewId:D} is not linked to any event (FOR_EVENT).")
            : MapSummary(records[0]);
    }

    private static CrewSummary MapSummary(IRecord record)
    {
        var id = Neo4jValueConversions.FromDatabaseId(record["CrewId"].As<string>());

        try
        {
            return new CrewSummary(
                id,
                Neo4jValueConversions.FromDatabaseId(record["EventId"].As<string>()),
                record["Name"].As<string>(),
                record["Description"].As<string>(),
                Convert.ToInt32(record["CurrentMembers"].As<long>()),
                Convert.ToInt32(record["MaxMembers"].As<long>()),
                record["Tags"].As<List<object>>().Select(tag => (string)tag).ToList(),
                new MeetingPoint(
                    record["MeetingPointName"].As<string>(),
                    record["MeetingPointLatitude"].As<double>(),
                    record["MeetingPointLongitude"].As<double>()),
                record["Joined"].As<bool>());
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidCastException)
        {
            throw new InvalidOperationException($"Crew {id:D} in Neo4j violates the data contract: {ex.Message}", ex);
        }
    }
}
