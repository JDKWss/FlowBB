using FlowBB.Application.Pulse;
using FlowBB.Domain.Common;
using FlowBB.Infrastructure.Neo4j;
using FluentAssertions;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Tests.Neo4j;

[Collection(Neo4jCollection.Name)]
public sealed class Neo4jPulseDataReaderTests(Neo4jFixture neo4j) : IAsyncLifetime
{
    private const double FirstLatitude = 49.82251;
    private const double FirstLongitude = 19.04441;
    private const double SecondLatitude = 49.81321;
    private const double SecondLongitude = 19.05071;
    private const double OtherLatitude = 49.79381;
    private const double OtherLongitude = 19.04955;

    private static readonly DateTimeOffset TargetEndAt = new(2026, 9, 20, 23, 15, 0, TimeSpan.FromHours(2));
    private static readonly DateTimeOffset OtherEndAt = new(2026, 9, 20, 21, 30, 0, TimeSpan.FromHours(2));

    private readonly string runId = $"pulse-reader-{Guid.NewGuid():N}";
    private readonly Guid targetEventId = Guid.NewGuid();
    private readonly Guid otherEventId = Guid.NewGuid();
    private readonly Guid emptyEventId = Guid.NewGuid();
    private readonly Guid firstUserId = Guid.NewGuid();
    private readonly Guid secondUserId = Guid.NewGuid();
    private readonly Guid otherUserId = Guid.NewGuid();

    private IDriver? driver;
    private Neo4jOptions? options;
    private Neo4jPulseDataReader? reader;

    public async Task InitializeAsync()
    {
        // Polaczenie i schemat naleza do wspolnego Neo4jFixture (zmienne FLOWBB_NEO4J_TEST_*); bez niego test jest pomijany.
        if (neo4j.Driver is null || neo4j.Options is null)
        {
            return;
        }

        driver = neo4j.Driver;
        options = neo4j.Options;
        await CreateFixtureAsync();
        reader = new Neo4jPulseDataReader(driver, options);
    }

    public async Task DisposeAsync()
    {
        if (driver is null || options is null)
        {
            return;
        }

        await driver.ExecutableQuery("MATCH (n {IntegrationTestRunId: $RunId}) DETACH DELETE n")
            .WithParameters(new { RunId = runId })
            .WithConfig(new QueryConfig(database: options.Database))
            .ExecuteAsync();
    }

    [Neo4jFact]
    public async Task GetPoints_ReturnsOnlyDeclarationsForRequestedEventWithoutUserId()
    {
        var points = await Reader.GetPointsAsync(targetEventId);

        points.Should().BeEquivalentTo(
        [
            new PulsePoint(FirstLatitude, FirstLongitude, TransportMode.Walking),
            new PulsePoint(SecondLatitude, SecondLongitude, TransportMode.PublicTransport)
        ]);
        typeof(PulsePoint).GetProperties().Select(property => property.Name)
            .Should().NotContain("UserId");

        var otherPoints = await Reader.GetPointsAsync(otherEventId);
        otherPoints.Should().ContainSingle()
            .Which.Should().Be(new PulsePoint(OtherLatitude, OtherLongitude, TransportMode.Car));
    }

    [Neo4jFact]
    public async Task GetPoints_ReturnsEmptyListForEventWithoutDeclarations()
    {
        var points = await Reader.GetPointsAsync(emptyEventId);

        points.Should().BeEmpty();
    }

    [Neo4jFact]
    public async Task GetPoints_ReturnsUpdatedTransportMode()
    {
        await ExecuteAsync("""
            MATCH (:User {UserId: $UserId})-[attendance:IS_GOING_TO]->(:Event {EventId: $EventId})
            SET attendance.TransportMode = 'Bike', attendance.UpdatedAt = datetime()
            """, new
        {
            UserId = firstUserId.ToString("D"),
            EventId = targetEventId.ToString("D")
        });

        var points = await Reader.GetPointsAsync(targetEventId);

        points.Single(point => point.Latitude == FirstLatitude)
            .TransportMode.Should().Be(TransportMode.Bike);
    }

    [Neo4jFact]
    public async Task GetPoints_DoesNotReturnDeletedDeclaration()
    {
        await ExecuteAsync("""
            MATCH (:User {UserId: $UserId})-[attendance:IS_GOING_TO]->(:Event {EventId: $EventId})
            DELETE attendance
            """, new
        {
            UserId = firstUserId.ToString("D"),
            EventId = targetEventId.ToString("D")
        });

        var points = await Reader.GetPointsAsync(targetEventId);

        points.Should().ContainSingle()
            .Which.Should().Be(new PulsePoint(
                SecondLatitude,
                SecondLongitude,
                TransportMode.PublicTransport));
    }

    [Neo4jFact]
    public async Task GetEvent_ReturnsEventAndNullForMissingEvent()
    {
        var found = await Reader.GetEventAsync(targetEventId);
        var missing = await Reader.GetEventAsync(Guid.NewGuid());

        found.Should().Be(new PulseEventInfo(targetEventId, "Pulse target event", TargetEndAt));
        missing.Should().BeNull();
    }

    [Neo4jFact]
    public async Task GetEvent_ReturnsEndAtWithOffsetAndNullWhenMissing()
    {
        var late = await Reader.GetEventAsync(targetEventId);
        var early = await Reader.GetEventAsync(otherEventId);
        var withoutEnd = await Reader.GetEventAsync(emptyEventId);

        late!.EndAt.Should().Be(TargetEndAt);
        late.EndAt!.Value.Offset.Should().Be(TimeSpan.FromHours(2));
        early!.EndAt.Should().Be(OtherEndAt);
        early.EndAt!.Value.Offset.Should().Be(TimeSpan.FromHours(2));
        withoutEnd!.EndAt.Should().BeNull();
    }

    [Neo4jFact]
    public async Task GetEvents_ReturnsFixtureEvents()
    {
        var events = await Reader.GetEventsAsync();

        events.Should().Contain(new PulseEventInfo(targetEventId, "Pulse target event", TargetEndAt));
        events.Should().Contain(new PulseEventInfo(otherEventId, "Pulse other event", OtherEndAt));
        events.Should().Contain(new PulseEventInfo(emptyEventId, "Pulse empty event"));
    }

    [Neo4jFact]
    public async Task GetEventsWithPoints_EqualsPerEventReads()
    {
        var events = await Reader.GetEventsAsync();
        var expected = new List<PulseEventSnapshot>();
        foreach (var info in events)
        {
            expected.Add(new PulseEventSnapshot(info, await Reader.GetPointsAsync(info.Id)));
        }

        var snapshots = await Reader.GetEventsWithPointsAsync();

        snapshots.Should().BeEquivalentTo(expected);
        typeof(PulseEventSnapshot).GetProperties().Select(property => property.Name)
            .Should().NotContain("UserId");
    }

    [Neo4jFact]
    public async Task GetEventsWithPoints_ExecutesOneQueryRegardlessOfEventCount()
    {
        var beforeFirstRead = Reader.ExecutedQueryCount;
        var initial = await Reader.GetEventsWithPointsAsync();
        var afterFirstRead = Reader.ExecutedQueryCount;
        await AddEventsAsync(12);

        var expanded = await Reader.GetEventsWithPointsAsync();

        (afterFirstRead - beforeFirstRead).Should().Be(1);
        (Reader.ExecutedQueryCount - afterFirstRead).Should().Be(1);
        expanded.Should().HaveCount(initial.Count + 12);
    }

    [Neo4jFact]
    public async Task GetPoints_InvalidCoordinatesAreNotIncludedInExceptionMessage()
    {
        const double invalidLatitude = 123.456789;
        await ExecuteAsync("""
            MATCH (:User {UserId: $UserId})-[attendance:IS_GOING_TO]->(:Event {EventId: $EventId})
            SET attendance.OriginLatitude = $InvalidLatitude
            """, new
        {
            UserId = firstUserId.ToString("D"),
            EventId = targetEventId.ToString("D"),
            InvalidLatitude = invalidLatitude
        });

        var action = () => Reader.GetPointsAsync(targetEventId);

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().NotContain(invalidLatitude.ToString());
        exception.Which.InnerException.Should().BeNull();
    }

    private Neo4jPulseDataReader Reader => reader ??
        throw new InvalidOperationException("Neo4j integration fixture has not been initialized.");

    private async Task CreateFixtureAsync()
    {
        const string query = """
            CREATE (target:Event:PulseReaderIntegrationTest {
              EventId: $TargetEventId,
              Name: 'Pulse target event',
              StartAt: datetime('2026-09-20T10:00:00Z'),
              EndAt: datetime('2026-09-20T23:15:00+02:00'),
              IntegrationTestRunId: $RunId
            })
            CREATE (other:Event:PulseReaderIntegrationTest {
              EventId: $OtherEventId,
              Name: 'Pulse other event',
              StartAt: datetime('2026-09-20T11:00:00Z'),
              EndAt: datetime('2026-09-20T21:30:00+02:00'),
              IntegrationTestRunId: $RunId
            })
            CREATE (empty:Event:PulseReaderIntegrationTest {
              EventId: $EmptyEventId,
              Name: 'Pulse empty event',
              StartAt: datetime('2026-09-20T12:00:00Z'),
              IntegrationTestRunId: $RunId
            })
            CREATE (first:User:PulseReaderIntegrationTest {
              UserId: $FirstUserId,
              IntegrationTestRunId: $RunId
            })
            CREATE (second:User:PulseReaderIntegrationTest {
              UserId: $SecondUserId,
              IntegrationTestRunId: $RunId
            })
            CREATE (outside:User:PulseReaderIntegrationTest {
              UserId: $OtherUserId,
              IntegrationTestRunId: $RunId
            })
            CREATE (first)-[:IS_GOING_TO {
              OriginLatitude: $FirstLatitude,
              OriginLongitude: $FirstLongitude,
              TransportMode: 'Walking',
              UpdatedAt: datetime('2026-09-20T08:00:00Z')
            }]->(target)
            CREATE (second)-[:IS_GOING_TO {
              OriginLatitude: $SecondLatitude,
              OriginLongitude: $SecondLongitude,
              TransportMode: 'PublicTransport',
              UpdatedAt: datetime('2026-09-20T08:01:00Z')
            }]->(target)
            CREATE (outside)-[:IS_GOING_TO {
              OriginLatitude: $OtherLatitude,
              OriginLongitude: $OtherLongitude,
              TransportMode: 'Car',
              UpdatedAt: datetime('2026-09-20T08:02:00Z')
            }]->(other)
            """;

        await ExecuteAsync(query, new
        {
            TargetEventId = targetEventId.ToString("D"),
            OtherEventId = otherEventId.ToString("D"),
            EmptyEventId = emptyEventId.ToString("D"),
            FirstUserId = firstUserId.ToString("D"),
            SecondUserId = secondUserId.ToString("D"),
            OtherUserId = otherUserId.ToString("D"),
            RunId = runId,
            FirstLatitude,
            FirstLongitude,
            SecondLatitude,
            SecondLongitude,
            OtherLatitude,
            OtherLongitude
        });
    }

    private async Task ExecuteAsync(string query, object parameters)
    {
        if (driver is null || options is null)
        {
            throw new InvalidOperationException("Neo4j integration fixture has not been initialized.");
        }

        await driver.ExecutableQuery(query)
            .WithParameters(parameters)
            .WithConfig(new QueryConfig(database: options.Database))
            .ExecuteAsync();
    }

    private Task AddEventsAsync(int count)
    {
        var eventIds = Enumerable.Range(0, count).Select(_ => Guid.NewGuid().ToString("D")).ToList();
        return ExecuteAsync(
            """
            UNWIND $EventIds AS eventId
            CREATE (:Event:PulseReaderIntegrationTest {
              EventId: eventId, Name: 'Bulk query test event', StartAt: datetime(), IntegrationTestRunId: $RunId
            })
            """,
            new { EventIds = eventIds, RunId = runId });
    }
}
