using System.Reflection;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

/// <summary>
/// Zaklada schemat (<c>database/schema.cypher</c>) i idempotentny seed demonstracyjny (<c>database/flowbb-demo-seed.cypher</c>).
/// Oba pliki sa osadzone w assembly, wiec inicjalizacja nie zalezy od katalogu roboczego.
/// </summary>
public static class Neo4jDatabaseInitializer
{
    public const string SeedOnStartupVariable = "NEO4J_SEED_ON_STARTUP";

    private const string SchemaResourceName = "FlowBB.Database.schema.cypher";
    private const string SeedResourceName = "FlowBB.Database.flowbb-demo-seed.cypher";
    private const string SeedEndMarker = "// __FLOWBB_SEED_END__";

    public static async Task InitializeAsync()
    {
        var options = Neo4jOptions.FromEnvironment();
        await using var driver = Neo4jDriverFactory.Create(options);
        await InitializeAsync(driver, options);
    }

    public static async Task InitializeAsync(IDriver driver, Neo4jOptions options)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(options);

        await driver.VerifyConnectivityAsync();
        await EnsureSchemaAsync(driver, options);
        await ApplySeedAsync(driver, options);
    }

    // Kazde polecenie schematu osobno (autocommit): Neo4j nie pozwala mieszac zmian schematu z zapisami w jednej transakcji.
    private static async Task EnsureSchemaAsync(IDriver driver, Neo4jOptions options)
    {
        foreach (var statement in LoadStatements(SchemaResourceName, endMarker: null))
        {
            await driver.ExecutableQuery(statement)
                .WithConfig(new QueryConfig(database: options.Database))
                .ExecuteAsync();
        }
    }

    // Caly seed w jednej transakcji: blad wycofuje wszystko, wiec baza nie zostaje z niepelnymi danymi.
    private static async Task ApplySeedAsync(IDriver driver, Neo4jOptions options)
    {
        var statements = LoadStatements(SeedResourceName, SeedEndMarker);

        await using var session = driver.AsyncSession(config => config.WithDatabase(options.Database));
        await session.ExecuteWriteAsync(async transaction =>
        {
            foreach (var statement in statements)
            {
                var cursor = await transaction.RunAsync(statement);
                await cursor.ConsumeAsync();
            }
        });
    }

    private static IReadOnlyList<string> LoadStatements(string resourceName, string? endMarker)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded Neo4j script {resourceName} was not found.");
        using var reader = new StreamReader(stream);
        var script = reader.ReadToEnd();

        if (endMarker is not null)
        {
            var markerIndex = script.IndexOf(endMarker, StringComparison.Ordinal);
            script = markerIndex < 0
                ? throw new InvalidOperationException($"End marker {endMarker} was not found in {resourceName}.")
                : script[..markerIndex];
        }

        var executableScript = string.Join(
            '\n',
            script
                .ReplaceLineEndings("\n")
                .Split('\n')
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        return executableScript
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
    }
}
