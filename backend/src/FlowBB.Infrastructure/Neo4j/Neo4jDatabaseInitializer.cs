using System.Reflection;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

public static class Neo4jDatabaseInitializer
{
    public const string SeedOnStartupVariable = "NEO4J_SEED_ON_STARTUP";

    private const string SeedResourceName = "FlowBB.Database.flowbb-demo-seed.cypher";
    private const string SeedEndMarker = "// __FLOWBB_SEED_END__";

    private static readonly string[] SchemaQueries =
    [
        "CREATE CONSTRAINT user_id_unique IF NOT EXISTS FOR (n:User) REQUIRE n.UserId IS UNIQUE",
        "CREATE CONSTRAINT user_email_unique IF NOT EXISTS FOR (n:User) REQUIRE n.Email IS UNIQUE",
        "CREATE CONSTRAINT event_id_unique IF NOT EXISTS FOR (n:Event) REQUIRE n.EventId IS UNIQUE",
        "CREATE CONSTRAINT venue_id_unique IF NOT EXISTS FOR (n:Venue) REQUIRE n.VenueId IS UNIQUE",
        "CREATE CONSTRAINT owner_id_unique IF NOT EXISTS FOR (n:BusinessOwner) REQUIRE n.OwnerId IS UNIQUE",
        "CREATE CONSTRAINT owner_email_unique IF NOT EXISTS FOR (n:BusinessOwner) REQUIRE n.Email IS UNIQUE",
        "CREATE CONSTRAINT tag_id_unique IF NOT EXISTS FOR (n:Tag) REQUIRE n.TagId IS UNIQUE",
        "CREATE CONSTRAINT crew_id_unique IF NOT EXISTS FOR (n:Crew) REQUIRE n.CrewId IS UNIQUE"
    ];

    public static async Task InitializeAsync()
    {
        var options = Neo4jOptions.FromEnvironment();
        await using var driver = Neo4jDriverFactory.Create(options);

        await RunAsync(driver, options.Database, "RETURN 1");
        foreach (var query in SchemaQueries)
        {
            await RunAsync(driver, options.Database, query);
        }

        await ApplySeedAsync(driver, options.Database, LoadSeedStatements());
    }

    private static async Task RunAsync(IDriver driver, string database, string query)
    {
        await driver.ExecutableQuery(query)
            .WithConfig(new QueryConfig(database: database))
            .ExecuteAsync();
    }

    private static async Task ApplySeedAsync(IDriver driver, string database, IReadOnlyList<string> statements)
    {
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
