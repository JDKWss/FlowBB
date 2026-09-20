using FlowBB.Infrastructure.Neo4j;
using FluentAssertions;

namespace FlowBB.Infrastructure.Tests.Neo4j;

[Collection(Neo4jCollection.Name)]
public sealed class Neo4jDatabaseInitializerTests(Neo4jFixture neo4j)
{
    private async Task<(long Nodes, long Relationships)> CountSeedDataAsync()
    {
        var rows = await neo4j.QueryAsync(
            """
            MATCH (n) WHERE n.TestRunId IS NULL AND NOT n:SchemaVersion
            WITH count(n) AS nodes
            OPTIONAL MATCH (a)-[r]->(b)
            WHERE a.TestRunId IS NULL AND b.TestRunId IS NULL
              AND NOT a:SchemaVersion AND NOT b:SchemaVersion
            RETURN nodes, count(r) AS relationships
            """);
        return (rows[0]["nodes"].As<long>(), rows[0]["relationships"].As<long>());
    }

    [Neo4jFact]
    public async Task InitializeAsync_IsIdempotentAndCreatesSchemaAndDemoSeed()
    {
        var existing = await neo4j.QueryAsync(
            "MATCH (n) WHERE n.TestRunId IS NULL AND NOT n:SchemaVersion RETURN collect(elementId(n)) AS ids");
        var existingIds = existing[0]["ids"].As<List<object>>().Select(id => (string)id).ToList();

        try
        {
            await Neo4jDatabaseInitializer.InitializeAsync(neo4j.Driver!, neo4j.Options!);
            var afterFirst = await CountSeedDataAsync();
            await Neo4jDatabaseInitializer.InitializeAsync(neo4j.Driver!, neo4j.Options!);
            var afterSecond = await CountSeedDataAsync();

            afterSecond.Should().Be(afterFirst, "seed jest idempotentny");
            var seeded = await neo4j.QueryAsync(
                "MATCH (u:User) WHERE u.TestRunId IS NULL WITH count(u) AS users MATCH (e:Event {Source: 'Demo'}) WHERE e.TestRunId IS NULL RETURN users, count(e) AS events");
            seeded[0]["users"].As<long>().Should().BeGreaterThanOrEqualTo(82, "seed demonstracyjny ma 82 uzytkownikow");
            seeded[0]["events"].As<long>().Should().BeGreaterThanOrEqualTo(4, "seed demonstracyjny ma 4 wydarzenia");

            var declared = await neo4j.QueryAsync("MATCH (:User)-[r:IS_GOING_TO]->(:Event) WHERE r.TransportMode IS NOT NULL AND r.OriginLatitude IS NOT NULL RETURN count(r) AS n");
            declared[0]["n"].As<long>().Should().BeGreaterThan(0, "kazda deklaracja ma snapshot");

            var schema = await neo4j.QueryAsync("SHOW INDEXES YIELD name RETURN collect(name) AS names");
            schema[0]["names"].As<List<object>>().Select(name => (string)name)
                .Should().Contain(
                    ["event_start_at", "event_id_unique", "user_id_unique", "crew_id_unique", "venue_id_unique", "schema_version_key_unique"]);
        }
        finally
        {
            // Sprzata wylacznie to, co zalozyl seed w tym tescie; dane sprzed testu zostaja.
            await neo4j.ExecuteAsync(
                "MATCH (n) WHERE n.TestRunId IS NULL AND NOT n:SchemaVersion AND NOT elementId(n) IN $existing DETACH DELETE n",
                new { existing = existingIds });
        }
    }
}
