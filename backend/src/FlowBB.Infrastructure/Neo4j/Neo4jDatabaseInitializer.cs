using System.Reflection;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

public static class Neo4jDatabaseInitializer
{
    public const string SeedOnStartupVariable = "NEO4J_SEED_ON_STARTUP";

    private const string SeedResourceName = "FlowBB.Database.flowbb-demo-seed.cypher";
    private const string SeedEndMarker = "// __FLOWBB_SEED_END__";

    public static async Task InitializeAsync()
    {
        await using var repository = Neo4jFlowBbGraphRepository.FromEnvironment();
        await repository.VerifyConnectivityAsync();
        await repository.EnsureSchemaAsync();
        await repository.ApplySeedAsync(LoadSeedStatements());
    }

    private static IReadOnlyList<string> LoadSeedStatements()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(SeedResourceName)
            ?? throw new InvalidOperationException($"Embedded Neo4j seed {SeedResourceName} was not found.");
        using var reader = new StreamReader(stream);
        var script = reader.ReadToEnd();
        var markerIndex = script.IndexOf(SeedEndMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
            throw new InvalidOperationException("Neo4j seed end marker was not found.");

        var executableScript = string.Join(
            '\n',
            script[..markerIndex]
                .ReplaceLineEndings("\n")
                .Split('\n')
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        return executableScript
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
    }
}

public sealed partial class Neo4jFlowBbGraphRepository
{
    internal async Task ApplySeedAsync(IReadOnlyList<string> statements)
    {
        ArgumentNullException.ThrowIfNull(statements);
        await using var session = driver.AsyncSession(config => config.WithDatabase(database));
        await session.ExecuteWriteAsync(async transaction =>
        {
            foreach (var statement in statements)
            {
                var cursor = await transaction.RunAsync(statement);
                await cursor.ConsumeAsync();
            }
        });
    }
}
