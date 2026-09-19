using FlowBB.Domain.Models;
using FlowBB.Infrastructure.Neo4j;
using Neo4j.Driver;

namespace FlowBB.Api.IntegrationTests.Neo4j;

public sealed class Neo4jFactAttribute : FactAttribute
{
    public Neo4jFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("FLOWBB_NEO4J_TESTS") != "1")
            Skip = "Set FLOWBB_NEO4J_TESTS=1 to run against the configured Neo4j database.";
    }
}

internal sealed class GraphTestData : IAsyncDisposable
{
    private readonly List<string> ids = [];
    private readonly IDriver driver;
    private readonly string database;
    public Neo4jFlowBbGraphRepository Repository { get; }

    public GraphTestData()
    {
        var options = Neo4jOptions.FromEnvironment();
        Repository = new Neo4jFlowBbGraphRepository(options);
        driver = GraphDatabase.Driver(options.Uri, AuthTokens.Basic(options.Username, options.Password));
        database = options.Database;
    }

    public Guid NewId()
    {
        var id = Guid.NewGuid();
        ids.Add(id.ToString("D"));
        return id;
    }

    public async Task<User> UserAsync()
    {
        var id = NewId();
        var user = new User(id, $"test-{id:D}@example.invalid", "SYNTHETIC-NOT-A-HASH", "TEST User", 0, 0);
        await Repository.UpsertUserAsync(user);
        return user;
    }

    public async Task<Event> EventAsync(int days = 30)
    {
        var item = new Event(NewId(), "TEST Event", "Synthetic integration test", "", DateTimeOffset.UtcNow.AddDays(days), null);
        await Repository.UpsertEventAsync(item);
        return item;
    }

    public async Task<Crew> CrewAsync(Guid eventId, int maxMembers = 4)
    {
        var crew = new Crew(NewId(), "TEST Crew", "Synthetic integration test", maxMembers, [], "TEST Point", 0, 0);
        await Repository.UpsertCrewAsync(crew);
        await Repository.AssignCrewToEventAsync(crew.CrewId, eventId);
        return crew;
    }

    public async Task<IReadOnlyList<IRecord>> QueryAsync(string query, object parameters)
    {
        var result = await driver.ExecutableQuery(query).WithParameters(parameters)
            .WithConfig(new QueryConfig(database: database)).ExecuteAsync();
        return result.Result.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            // Only fresh, randomly generated IDs belonging to this test are deleted.
            await QueryAsync("""
                MATCH (n) WHERE (n:User AND n.UserId IN $Ids) OR (n:Event AND n.EventId IN $Ids)
                  OR (n:Crew AND n.CrewId IN $Ids) OR (n:Venue AND n.VenueId IN $Ids)
                  OR (n:BusinessOwner AND n.OwnerId IN $Ids) OR (n:Tag AND n.TagId IN $Ids)
                DETACH DELETE n
                """, new { Ids = ids });
        }
        finally
        {
            await Repository.DisposeAsync();
            await driver.DisposeAsync();
        }
    }
}
