using System.Globalization;
using System.Reflection;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

internal static class Neo4jSchemaMigrator
{
    private const string ResourcePrefix = "FlowBB.Database.Migrations.";
    private const string VersionKey = "flowbb";

    public static async Task ApplyAsync(IDriver driver, string database)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentException.ThrowIfNullOrWhiteSpace(database);

        var migrations = GetMigrations();
        var currentVersion = await ReadCurrentVersionAsync(driver, database);
        if (currentVersion > migrations[^1].Version)
        {
            throw new InvalidOperationException(
                $"Neo4j schema version {currentVersion} is newer than supported version {migrations[^1].Version}.");
        }

        foreach (var migration in migrations.Where(item => item.Version > currentVersion))
        {
            await ApplyMigrationAsync(driver, database, migration);
        }
    }

    internal static IReadOnlyList<SchemaMigration> GetMigrations()
    {
        var resources = Assembly.GetExecutingAssembly().GetManifestResourceNames();
        var migrations = resources
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(".cypher", StringComparison.Ordinal))
            .Select(CreateMigration)
            .OrderBy(item => item.Version)
            .ToArray();

        ValidateSequence(migrations);
        return migrations;
    }

    private static async Task ApplyMigrationAsync(IDriver driver, string database, SchemaMigration migration)
    {
        foreach (var statement in Neo4jScriptLoader.LoadStatements(migration.ResourceName))
        {
            await driver.ExecutableQuery(statement)
                .WithConfig(new QueryConfig(database: database))
                .ExecuteAsync();
        }

        var appliedVersion = await ReadCurrentVersionAsync(driver, database);
        if (appliedVersion != migration.Version)
        {
            throw new InvalidOperationException(
                $"Neo4j migration {migration.Name} did not set schema version {migration.Version}.");
        }
    }

    private static async Task<int> ReadCurrentVersionAsync(IDriver driver, string database)
    {
        const string query = "MATCH (version:SchemaVersion {Key: $key}) RETURN version.Version AS version";
        var result = await driver.ExecutableQuery(query)
            .WithParameters(new { key = VersionKey })
            .WithConfig(new QueryConfig(database: database))
            .ExecuteAsync();

        return result.Result.Count == 0 ? 0 : checked((int)result.Result[0]["version"].As<long>());
    }

    private static SchemaMigration CreateMigration(string resourceName)
    {
        var name = resourceName[ResourcePrefix.Length..^".cypher".Length];
        var separatorIndex = name.IndexOf('_', StringComparison.Ordinal);
        var versionText = separatorIndex < 0 ? name : name[..separatorIndex];
        if (!int.TryParse(versionText, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
        {
            throw new InvalidOperationException($"Neo4j migration {name} must start with a numeric version.");
        }

        return new SchemaMigration(version, name, resourceName);
    }

    private static void ValidateSequence(IReadOnlyList<SchemaMigration> migrations)
    {
        if (migrations.Count == 0)
        {
            throw new InvalidOperationException("No embedded Neo4j schema migrations were found.");
        }

        for (var index = 0; index < migrations.Count; index++)
        {
            if (migrations[index].Version != index + 1)
            {
                throw new InvalidOperationException("Neo4j schema migration versions must be unique and consecutive from 001.");
            }
        }
    }

    internal sealed record SchemaMigration(int Version, string Name, string ResourceName);
}
