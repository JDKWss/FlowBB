using FlowBB.Domain.Repositories;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

public sealed partial class Neo4jFlowBbGraphRepository : IFlowBbGraphRepository, IAsyncDisposable
{
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

    private readonly IDriver driver;
    private readonly string database;

    public Neo4jFlowBbGraphRepository(Neo4jOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        database = options.Database;
        driver = GraphDatabase.Driver(
            options.Uri,
            AuthTokens.Basic(options.Username, options.Password));
    }

    public static Neo4jFlowBbGraphRepository FromEnvironment()
    {
        return new Neo4jFlowBbGraphRepository(Neo4jOptions.FromEnvironment());
    }

    public Task VerifyConnectivityAsync()
    {
        return ExecuteAsync("RETURN 1", new { });
    }

    public async Task EnsureSchemaAsync()
    {
        foreach (var query in SchemaQueries)
        {
            await ExecuteAsync(query, new { });
        }
    }

    public ValueTask DisposeAsync()
    {
        return driver.DisposeAsync();
    }

    private static string ToDatabaseId(Guid id)
    {
        return id.ToString("D");
    }

    private static Guid FromDatabaseId(string id)
    {
        return Guid.ParseExact(id, "D");
    }

    private async Task ExecuteAsync(string query, object parameters)
    {
        await driver.ExecutableQuery(query)
            .WithParameters(parameters)
            .WithConfig(new QueryConfig(database: database))
            .ExecuteAsync();
    }

    private async Task<T?> ExecuteSingleAsync<T>(
        string query,
        object parameters,
        Func<IRecord, T> map)
        where T : class
    {
        var result = await driver.ExecutableQuery(query)
            .WithParameters(parameters)
            .WithConfig(new QueryConfig(database: database, routing: RoutingControl.Readers))
            .ExecuteAsync();

        var record = result.Result.SingleOrDefault();
        return record is null ? null : map(record);
    }

    private async Task ExecuteRelationshipAsync(
        string query,
        object parameters,
        string missingNodesMessage)
    {
        var result = await driver.ExecutableQuery(query)
            .WithParameters(parameters)
            .WithConfig(new QueryConfig(database: database))
            .ExecuteAsync();

        var matchedNodes = result.Result.Single().Get<long>("Matches");
        if (matchedNodes == 0)
        {
            throw new InvalidOperationException(missingNodesMessage);
        }
    }

    private async Task<IReadOnlyList<T>> ExecuteListAsync<T>(
        string query, object parameters, Func<IRecord, T> map)
    {
        var result = await driver.ExecutableQuery(query)
            .WithParameters(parameters)
            .WithConfig(new QueryConfig(database: database, routing: RoutingControl.Readers))
            .ExecuteAsync();
        return result.Result.Select(map).ToArray();
    }

    private async Task<bool> ExecuteBooleanAsync(string query, object parameters)
    {
        var result = await driver.ExecutableQuery(query)
            .WithParameters(parameters)
            .WithConfig(new QueryConfig(database: database))
            .ExecuteAsync();
        return result.Result.Single().Get<bool>("Success");
    }

    private static void ValidateLimit(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 100);
    }
}
