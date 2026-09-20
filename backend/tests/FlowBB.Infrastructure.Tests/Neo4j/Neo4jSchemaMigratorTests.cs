using FlowBB.Infrastructure.Neo4j;
using FluentAssertions;

namespace FlowBB.Infrastructure.Tests.Neo4j;

[Collection(Neo4jCollection.Name)]
public sealed class Neo4jSchemaMigratorTests(Neo4jFixture neo4j)
{
    [Neo4jFact]
    public async Task ApplyAsync_Twice_IsIdempotentAndRecordsLatestVersion()
    {
        await neo4j.ExecuteAsync("MATCH (version:SchemaVersion {Key: 'flowbb'}) DELETE version");

        await Neo4jSchemaMigrator.ApplyAsync(neo4j.Driver!, neo4j.Options!.Database);
        var afterFirst = await ReadSchemaStateAsync();
        await Neo4jSchemaMigrator.ApplyAsync(neo4j.Driver!, neo4j.Options.Database);
        var afterSecond = await ReadSchemaStateAsync();

        afterSecond.Should().BeEquivalentTo(afterFirst, options => options.WithStrictOrdering());
        afterSecond.Versions.Should().ContainSingle().Which.Should().Be("2:002_event_start_at_index");
        afterSecond.Indexes.Should().Contain(["event_start_at", "event_id_unique", "user_id_unique", "crew_id_unique"]);
    }

    [Neo4jFact]
    public async Task ApplyAsync_FromVersionOne_AppliesTheNextMigration()
    {
        await SetVersionAsync(1, "001_constraints");
        await neo4j.ExecuteAsync("DROP INDEX event_start_at IF EXISTS");

        await Neo4jSchemaMigrator.ApplyAsync(neo4j.Driver!, neo4j.Options!.Database);

        var state = await ReadSchemaStateAsync();
        state.Versions.Should().ContainSingle().Which.Should().Be("2:002_event_start_at_index");
        state.Indexes.Should().Contain("event_start_at");
    }

    [Neo4jFact]
    public async Task ApplyAsync_WhenDatabaseVersionIsNewer_ThrowsAndPreservesMarker()
    {
        await SetVersionAsync(3, "003_future_schema");

        try
        {
            var action = () => Neo4jSchemaMigrator.ApplyAsync(neo4j.Driver!, neo4j.Options!.Database);

            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Neo4j schema version 3 is newer than supported version 2.");
            (await ReadSchemaStateAsync()).Versions.Should().ContainSingle().Which.Should().Be("3:003_future_schema");
        }
        finally
        {
            await SetVersionAsync(2, "002_event_start_at_index");
        }
    }

    private async Task<SchemaState> ReadSchemaStateAsync()
    {
        var indexes = await neo4j.QueryAsync("SHOW INDEXES YIELD name RETURN name ORDER BY name");
        var constraints = await neo4j.QueryAsync("SHOW CONSTRAINTS YIELD name RETURN name ORDER BY name");
        var versions = await neo4j.QueryAsync(
            "MATCH (version:SchemaVersion {Key: 'flowbb'}) RETURN version.Version AS version, version.Name AS name");

        return new SchemaState(
            indexes.Select(row => row["name"].As<string>()).ToArray(),
            constraints.Select(row => row["name"].As<string>()).ToArray(),
            versions.Select(row => $"{row["version"].As<long>()}:{row["name"].As<string>()}").ToArray());
    }

    private Task SetVersionAsync(int version, string name)
    {
        return neo4j.ExecuteAsync(
            "MATCH (marker:SchemaVersion {Key: 'flowbb'}) SET marker.Version = $version, marker.Name = $name",
            new { version, name });
    }

    private sealed record SchemaState(
        IReadOnlyList<string> Indexes,
        IReadOnlyList<string> Constraints,
        IReadOnlyList<string> Versions);
}
