using System.Collections.Concurrent;
using System.Diagnostics;
using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Pulse.GetActivityMap;
using FlowBB.Application.Pulse.GetPulseHexagons;
using FlowBB.Application.Pulse.GetPulseSummary;
using FlowBB.Domain.Attendance;
using FlowBB.Domain.Common;
using FlowBB.Infrastructure.Neo4j;
using FluentAssertions;
using Xunit.Abstractions;

namespace FlowBB.Infrastructure.Tests.Neo4j;

[Collection(Neo4jCollection.Name)]
public sealed class Neo4jPulseLoadTests(Neo4jFixture neo4j, ITestOutputHelper output)
{
    private const int MainUsers = 80;
    private const int HiddenCellUsers = 9;
    private const int EventCount = 4;
    private const int ReadOperations = 40;
    private const int MaxWriteConcurrency = 12;
    private const double MinimumThroughput = 5;
    private const double MaximumWriteP95Milliseconds = 5000;
    private const double MaximumReadP95Milliseconds = 3000;
    private readonly string loadRunId = $"pulse-load-{Guid.NewGuid():N}";

    [Neo4jFact]
    public async Task ConcurrentAttendanceWritesAndPulseReads_MeetSafetyAndPerformanceThresholds()
    {
        try
        {
            await RunLoadScenarioAsync();
        }
        finally
        {
            await neo4j.ExecuteAsync("MATCH (n {LoadTestRunId: $runId}) DETACH DELETE n", new { runId = loadRunId });
        }
    }

    private async Task RunLoadScenarioAsync()
    {
        var repository = new Neo4jAttendanceRepository(neo4j.Driver!, neo4j.Options!);
        var reader = new Neo4jPulseDataReader(neo4j.Driver!, neo4j.Options!);
        var baseline = (await new GetPulseSummaryHandler(reader, TimeProvider.System).HandleAsync()).ParticipantsCount;
        var workload = await CreateWorkloadAsync();
        var metrics = new LoadMetrics();
        var elapsed = Stopwatch.StartNew();
        var firstWrites = RunWritesAsync(repository, workload.Intents, metrics);
        var reads = RunReadsAsync(reader, workload.TargetEventId, metrics);
        var firstResults = await firstWrites;
        await reads;
        var repeatedResults = await RunWritesAsync(repository, workload.Intents, metrics);
        elapsed.Stop();

        var snapshot = metrics.Snapshot(firstResults.Count + repeatedResults.Count + ReadOperations, elapsed.Elapsed);
        output.WriteLine(snapshot.ToString());
        await AssertFinalStateAsync(reader, workload, baseline, firstResults, repeatedResults);
        AssertThresholds(snapshot);
    }

    private async Task<Workload> CreateWorkloadAsync()
    {
        var eventIds = Enumerable.Range(0, EventCount).Select(_ => Guid.NewGuid()).ToArray();
        var users = BuildUsers(eventIds);
        await neo4j.ExecuteAsync(
            "UNWIND $events AS row CREATE (:Event {EventId: row.id, Name: row.name, StartAt: datetime(), LoadTestRunId: $runId})",
            new { events = eventIds.Select((id, index) => new { id = id.ToString("D"), name = $"Synthetic load event {index + 1}" }), runId = loadRunId });
        await neo4j.ExecuteAsync(
            "UNWIND $users AS row CREATE (:User {UserId: row.id, Name: 'Synthetic load user', DefaultOriginLatitude: row.latitude, DefaultOriginLongitude: row.longitude, LoadTestRunId: $runId})",
            new { users = users.Select(ToUserParameter), runId = loadRunId });

        var timestamp = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var intents = users.Select(user => new AttendanceIntent(user.EventId, user.Id, user.Mode, timestamp)).ToArray();
        return new Workload(eventIds[0], intents, MainUsers + HiddenCellUsers, MainUsers / EventCount);
    }

    private static IReadOnlyList<LoadUser> BuildUsers(IReadOnlyList<Guid> eventIds)
    {
        var modes = new[] { TransportMode.Walking, TransportMode.Bike, TransportMode.Car, TransportMode.PublicTransport };
        var users = Enumerable.Range(0, MainUsers)
            .Select(index => new LoadUser(Guid.NewGuid(), eventIds[index % eventIds.Count], 49.8225, 19.0444, modes[index % modes.Length]))
            .ToList();
        users.AddRange(Enumerable.Range(0, HiddenCellUsers)
            .Select(index => new LoadUser(Guid.NewGuid(), eventIds[0], 49.95, 19.25, modes[index % modes.Length])));
        return users;
    }

    private static object ToUserParameter(LoadUser user) => new
    {
        id = user.Id.ToString("D"),
        latitude = user.Latitude,
        longitude = user.Longitude
    };

    private static async Task<IReadOnlyList<AttendanceUpsertPersistenceResult>> RunWritesAsync(
        IAttendanceRepository repository,
        IReadOnlyList<AttendanceIntent> intents,
        LoadMetrics metrics)
    {
        var results = new ConcurrentBag<AttendanceUpsertPersistenceResult>();
        await Parallel.ForEachAsync(
            intents,
            new ParallelOptions { MaxDegreeOfParallelism = MaxWriteConcurrency },
            async (intent, token) =>
            {
                var started = Stopwatch.GetTimestamp();
                var result = await repository.UpsertAsync(intent, token);
                metrics.AddWrite(Stopwatch.GetElapsedTime(started));
                results.Add(result ?? throw new InvalidOperationException("Synthetic load data was not found."));
            });
        return results.ToArray();
    }

    private static async Task RunReadsAsync(Neo4jPulseDataReader reader, Guid eventId, LoadMetrics metrics)
    {
        var summary = new GetPulseSummaryHandler(reader, TimeProvider.System);
        var hexagons = new GetPulseHexagonsHandler(reader, new GetActivityMapHandler(reader));
        await Parallel.ForEachAsync(
            Enumerable.Range(0, ReadOperations),
            new ParallelOptions { MaxDegreeOfParallelism = 8 },
            async (index, token) =>
            {
                var started = Stopwatch.GetTimestamp();
                _ = index % 2 == 0
                    ? (object)await summary.HandleAsync(token)
                    : await hexagons.HandleAsync(eventId, token) ?? [];
                metrics.AddRead(Stopwatch.GetElapsedTime(started));
            });
    }

    private async Task AssertFinalStateAsync(
        Neo4jPulseDataReader reader,
        Workload workload,
        int baselineParticipants,
        IReadOnlyList<AttendanceUpsertPersistenceResult> first,
        IReadOnlyList<AttendanceUpsertPersistenceResult> repeated)
    {
        first.Should().OnlyContain(result => result.IsNew);
        repeated.Should().OnlyContain(result => !result.IsNew);
        var relationships = await CountRelationshipsAsync();
        relationships.Should().Be(workload.ExpectedParticipants);

        var summary = await new GetPulseSummaryHandler(reader, TimeProvider.System).HandleAsync();
        summary.ParticipantsCount.Should().Be(baselineParticipants + workload.ExpectedParticipants);
        var cells = await new GetPulseHexagonsHandler(reader, new GetActivityMapHandler(reader))
            .HandleAsync(workload.TargetEventId);
        cells.Should().ContainSingle().Which.Participants.Should().Be(workload.ExpectedVisibleCellParticipants);
    }

    private async Task<int> CountRelationshipsAsync()
    {
        var rows = await neo4j.QueryAsync(
            "MATCH (u:User {LoadTestRunId: $runId})-[r:IS_GOING_TO]->(e:Event {LoadTestRunId: $runId}) RETURN count(r) AS total",
            new { runId = loadRunId });
        return checked((int)rows[0]["total"].As<long>());
    }

    private static void AssertThresholds(LoadSnapshot snapshot)
    {
        snapshot.Throughput.Should().BeGreaterThanOrEqualTo(MinimumThroughput);
        snapshot.WriteP95Milliseconds.Should().BeLessThanOrEqualTo(MaximumWriteP95Milliseconds);
        snapshot.ReadP95Milliseconds.Should().BeLessThanOrEqualTo(MaximumReadP95Milliseconds);
    }

    private sealed record LoadUser(Guid Id, Guid EventId, double Latitude, double Longitude, TransportMode Mode);

    private sealed record Workload(
        Guid TargetEventId,
        IReadOnlyList<AttendanceIntent> Intents,
        int ExpectedParticipants,
        int ExpectedVisibleCellParticipants);

    private sealed class LoadMetrics
    {
        private readonly ConcurrentBag<double> writeMilliseconds = [];
        private readonly ConcurrentBag<double> readMilliseconds = [];

        public void AddWrite(TimeSpan elapsed) => writeMilliseconds.Add(elapsed.TotalMilliseconds);

        public void AddRead(TimeSpan elapsed) => readMilliseconds.Add(elapsed.TotalMilliseconds);

        public LoadSnapshot Snapshot(int operations, TimeSpan elapsed) => new(
            operations / elapsed.TotalSeconds,
            Percentile(writeMilliseconds, 0.5),
            Percentile(writeMilliseconds, 0.95),
            Percentile(readMilliseconds, 0.5),
            Percentile(readMilliseconds, 0.95));

        private static double Percentile(IEnumerable<double> values, double percentile)
        {
            var ordered = values.Order().ToArray();
            var index = Math.Max(0, (int)Math.Ceiling(percentile * ordered.Length) - 1);
            return ordered[index];
        }
    }

    private sealed record LoadSnapshot(
        double Throughput,
        double WriteMedianMilliseconds,
        double WriteP95Milliseconds,
        double ReadMedianMilliseconds,
        double ReadP95Milliseconds)
    {
        public override string ToString() => FormattableString.Invariant(
            $"PULSE_LOAD throughput={Throughput:F1} ops/s write_median={WriteMedianMilliseconds:F1} ms write_p95={WriteP95Milliseconds:F1} ms read_median={ReadMedianMilliseconds:F1} ms read_p95={ReadP95Milliseconds:F1} ms");
    }
}
