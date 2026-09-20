using FlowBB.Application.Pulse;
using FlowBB.Infrastructure.Neo4j;
using FluentAssertions;

namespace FlowBB.Infrastructure.Tests.Neo4j;

/// <summary>
/// Seed demo (<c>database/flowbb-demo-seed.cypher</c>) wykonany na prawdziwym Neo4j. Testy wymagaja jednorazowej instancji:
/// seed przywraca stan demonstracyjny, wiec kasuje deklaracje i czlonkostwa seedowanych uzytkownikow. Jesli seed byl
/// w bazie przed testem, zostaje w niej; w przeciwnym razie test usuwa go po sobie.
/// </summary>
[Collection(Neo4jCollection.Name)]
public sealed class Neo4jDemoSeedTests(Neo4jFixture neo4j)
{
    private static readonly Guid EarlyEventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LateEventId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private const string SeededNodes = """
        (n:User AND (n.UserId IN ['aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'dddddddd-dddd-dddd-dddd-dddddddddddd'] OR n.UserId STARTS WITH 'd1000000-'))
        OR (n:Event AND n.EventId IN [
              '11111111-1111-1111-1111-111111111111', '33333333-3333-3333-3333-333333333333',
              '44444444-4444-4444-4444-444444444444', '55555555-5555-5555-5555-555555555555'])
        OR (n:Crew AND n.CrewId IN ['22222222-2222-2222-2222-222222222222', '66666666-6666-6666-6666-666666666666'])
        OR (n:Venue AND n.VenueId STARTS WITH 'seed-venue-')
        OR (n:BusinessOwner AND n.OwnerId = 'seed-owner-flowbb')
        OR (n:Tag AND n.TagId STARTS WITH 'seed-tag-')
        """;

    [Neo4jFact]
    public async Task Seed_AppliedTwice_GivesTheSameState()
    {
        await WithSeedAsync(async () =>
        {
            var first = await SnapshotAsync();
            await Neo4jDatabaseInitializer.ApplySeedAsync(neo4j.Driver!, neo4j.Options!.Database);
            var second = await SnapshotAsync();

            second.Should().Equal(first);
            first.Should().Contain(line => line.StartsWith("Event=4", StringComparison.Ordinal));
            first.Should().Contain(line => line.StartsWith("attendance 11111111", StringComparison.Ordinal) && line.EndsWith("=82", StringComparison.Ordinal));
        });
    }

    [Neo4jFact]
    public async Task Seed_ContainsLateEventAfterTenPmAndAnEarlierOne()
    {
        await WithSeedAsync(async () =>
        {
            var reader = new Neo4jPulseDataReader(neo4j.Driver!, neo4j.Options!);

            var late = await reader.GetEventAsync(LateEventId);
            var early = await reader.GetEventAsync(EarlyEventId);

            late!.EndAt.Should().Be(new DateTimeOffset(2026, 9, 20, 23, 15, 0, TimeSpan.FromHours(2)));
            early!.EndAt.Should().Be(new DateTimeOffset(2026, 9, 19, 21, 30, 0, TimeSpan.FromHours(2)));
            DemoReturnGapPolicy.IsLateEvent(late.EndAt).Should().BeTrue();
            DemoReturnGapPolicy.IsLateEvent(early.EndAt).Should().BeFalse();
        });
    }

    private async Task WithSeedAsync(Func<Task> assertions)
    {
        var existedBefore = await CountSeededNodesAsync() > 0;
        await Neo4jDatabaseInitializer.ApplySeedAsync(neo4j.Driver!, neo4j.Options!.Database);
        try
        {
            await assertions();
        }
        finally
        {
            if (!existedBefore)
            {
                await neo4j.ExecuteAsync($"MATCH (n) WHERE {SeededNodes} DETACH DELETE n");
            }
        }
    }

    private async Task<long> CountSeededNodesAsync()
    {
        var records = await neo4j.QueryAsync($"MATCH (n) WHERE {SeededNodes} RETURN count(n) AS Nodes");
        return records[0]["Nodes"].As<long>();
    }

    // Deterministyczny opis stanu seedu: liczby wezlow, uczestnicy i czlonkowie oraz koniec kazdego seedowanego wydarzenia.
    private async Task<IReadOnlyList<string>> SnapshotAsync()
    {
        var nodes = await neo4j.QueryAsync($"""
            MATCH (n) WHERE {SeededNodes}
            RETURN labels(n)[0] AS Label, count(*) AS Count ORDER BY Label
            """);
        var attendance = await neo4j.QueryAsync("""
            MATCH (e:Event) WHERE e.EventId IN ['11111111-1111-1111-1111-111111111111', '33333333-3333-3333-3333-333333333333',
                                                '44444444-4444-4444-4444-444444444444', '55555555-5555-5555-5555-555555555555']
            OPTIONAL MATCH (:User)-[r:IS_GOING_TO]->(e)
            RETURN e.EventId AS Id, toString(e.EndAt) AS EndAt, count(r) AS Count ORDER BY Id
            """);
        var members = await neo4j.QueryAsync("""
            MATCH (c:Crew) WHERE c.CrewId IN ['22222222-2222-2222-2222-222222222222', '66666666-6666-6666-6666-666666666666']
            OPTIONAL MATCH (:User)-[m:MEMBER_OF]->(c)
            RETURN c.CrewId AS Id, count(m) AS Count ORDER BY Id
            """);

        return nodes.Select(r => $"{r.Get<string>("Label")}={r.Get<long>("Count")}")
            .Concat(attendance.Select(r => $"attendance {r.Get<string>("Id")[..8]} end {r.Get<string>("EndAt")}={r.Get<long>("Count")}"))
            .Concat(members.Select(r => $"members {r.Get<string>("Id")[..8]}={r.Get<long>("Count")}"))
            .ToList();
    }
}
