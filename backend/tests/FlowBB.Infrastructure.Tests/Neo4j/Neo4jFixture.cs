using FlowBB.Infrastructure.Neo4j;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Tests.Neo4j;

[CollectionDefinition(Name)]
public sealed class Neo4jCollection : ICollectionFixture<Neo4jFixture>
{
    public const string Name = "Neo4j";
}

/// <summary>
/// Wspolne polaczenie z prawdziwym Neo4j. Stosuje prawdziwy <c>database/schema.cypher</c> i sprzata dane testowe:
/// kazdy wezel utworzony przez pomocnicze metody ma <c>TestRunId</c>, po ktorym jest usuwany na koncu przebiegu.
/// </summary>
public sealed class Neo4jFixture : IAsyncLifetime
{
    private readonly string runId = Guid.NewGuid().ToString("N");

    public Neo4jOptions? Options { get; private set; }

    public IDriver? Driver { get; private set; }

    public async Task InitializeAsync()
    {
        if (!Neo4jTestEnvironment.IsConfigured)
        {
            return;
        }

        Options = Neo4jTestEnvironment.Load();
        Driver = Neo4jDriverFactory.Create(Options);
        await Driver.VerifyConnectivityAsync();

        foreach (var statement in ReadSchemaStatements())
        {
            await ExecuteAsync(statement);
        }
    }

    public async Task DisposeAsync()
    {
        if (Driver is null)
        {
            return;
        }

        await ExecuteAsync("MATCH (n {TestRunId: $runId}) DETACH DELETE n", new { runId });
        await Driver.DisposeAsync();
    }

    public async Task ExecuteAsync(string query, object? parameters = null)
    {
        await Driver!.ExecutableQuery(query)
            .WithParameters(parameters ?? new { })
            .WithConfig(new QueryConfig(database: Options!.Database))
            .ExecuteAsync();
    }

    public async Task<IReadOnlyList<IRecord>> QueryAsync(string query, object? parameters = null)
    {
        var result = await Driver!.ExecutableQuery(query)
            .WithParameters(parameters ?? new { })
            .WithConfig(new QueryConfig(database: Options!.Database))
            .ExecuteAsync();
        return result.Result;
    }

    public async Task<Guid> CreateUserAsync(double latitude = 49.8225, double longitude = 19.0444)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            """
            CREATE (:User {UserId: $id, Name: 'Test User', HomeLatitude: $latitude, HomeLongitude: $longitude, TestRunId: $runId})
            """,
            new { id = id.ToString("D"), latitude, longitude, runId });
        return id;
    }

    public async Task<string> CreateVenueAsync(string name = "Test Venue", double latitude = 49.82245, double longitude = 19.04431)
    {
        var id = $"test-venue-{Guid.NewGuid():N}";
        await ExecuteAsync(
            """
            CREATE (:Venue {VenueId: $id, Name: $name, Address: 'Test', Latitude: $latitude, Longitude: $longitude, TestRunId: $runId})
            """,
            new { id, name, latitude, longitude, runId });
        return id;
    }

    public async Task<Guid> CreateEventAsync(
        string venueId,
        DateTimeOffset startAt,
        DateTimeOffset? endAt = null,
        string name = "Test Event",
        string category = "Culture",
        string source = "Demo")
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            """
            MATCH (v:Venue {VenueId: $venueId})
            CREATE (e:Event {EventId: $id, Name: $name, Description: 'Opis', EventUrl: 'https://example.invalid/e',
                             Category: $category, Source: $source, StartAt: $startAt, EndAt: $endAt, TestRunId: $runId})
            CREATE (e)-[:HOSTED_AT]->(v)
            """,
            new { id = id.ToString("D"), venueId, name, category, source, startAt, endAt, runId });
        return id;
    }

    public Task DeclareGoingAsync(Guid userId, Guid eventId)
    {
        return ExecuteAsync(
            """
            MATCH (u:User {UserId: $userId}), (e:Event {EventId: $eventId})
            CREATE (u)-[:IS_GOING_TO {TransportMode: 'Walking', OriginLatitude: u.HomeLatitude,
                                       OriginLongitude: u.HomeLongitude, UpdatedAt: datetime()}]->(e)
            """,
            new { userId = userId.ToString("D"), eventId = eventId.ToString("D") });
    }

    private static IEnumerable<string> ReadSchemaStatements()
    {
        var path = FindRepositoryFile(Path.Combine("database", "schema.cypher"));
        var withoutComments = string.Join(
            '\n',
            File.ReadAllLines(path).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        return withoutComments
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(statement => statement.Length > 0);
    }

    private static string FindRepositoryFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Nie znaleziono {relativePath} powyzej {AppContext.BaseDirectory}.");
    }
}
