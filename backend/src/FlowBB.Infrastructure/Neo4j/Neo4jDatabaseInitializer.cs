using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

/// <summary>
/// Stosuje wersjonowane migracje schematu i idempotentny seed demonstracyjny.
/// Skrypty sa osadzone w assembly, wiec inicjalizacja nie zalezy od katalogu roboczego.
/// </summary>
public static class Neo4jDatabaseInitializer
{
    public const string SeedOnStartupVariable = "NEO4J_SEED_ON_STARTUP";

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
        await Neo4jSchemaMigrator.ApplyAsync(driver, options.Database);
        await ApplySeedAsync(driver, options.Database);
    }

    internal static Task ApplySeedAsync(IDriver driver, string database) =>
        Neo4jScriptLoader.ApplyInTransactionAsync(
            driver,
            database,
            Neo4jScriptLoader.LoadStatements(SeedResourceName, SeedEndMarker));
}
