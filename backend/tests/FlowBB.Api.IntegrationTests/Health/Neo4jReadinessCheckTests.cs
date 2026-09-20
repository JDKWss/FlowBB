using System.Diagnostics;
using FlowBB.Api.Health;
using FlowBB.Api.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace FlowBB.Api.IntegrationTests.Health;

public sealed class Neo4jReadinessCheckTests
{
    private const string Secret = "Cannot connect to neo4j+s://user:hunter2@secret-host.example:7687";

    private static (Neo4jReadinessCheck Check, ListLogger<Neo4jReadinessCheck> Logger) Create(
        Func<Task> verifyConnectivity, TimeSpan? timeout = null)
    {
        var logger = new ListLogger<Neo4jReadinessCheck>();
        return (new Neo4jReadinessCheck(FakeDriverProxy.Create(verifyConnectivity), logger, timeout), logger);
    }

    [Fact]
    public async Task ReachableDatabase_IsHealthyAndLogsNothing()
    {
        var (check, logger) = Create(() => Task.CompletedTask);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ConnectionFailure_IsUnhealthyWithGenericDescriptionAndNoException()
    {
        var (check, _) = Create(() => Task.FromException(new InvalidOperationException(Secret)));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be(Neo4jReadinessCheck.NotReadyDescription);
        result.Exception.Should().BeNull("the exception must not reach the response");
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task ConnectionFailure_IsLoggedOnceAsWarningWithTheException()
    {
        var failure = new InvalidOperationException(Secret);
        var (check, logger) = Create(() => Task.FromException(failure));

        await check.CheckHealthAsync(new HealthCheckContext());

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Exception.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task HangingConnection_IsUnhealthyWithinTheTimeout()
    {
        var (check, logger) = Create(() => new TaskCompletionSource().Task, TimeSpan.FromMilliseconds(150));
        var stopwatch = Stopwatch.StartNew();

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));
        logger.Entries.Should().ContainSingle().Which.Exception.Should().BeOfType<TimeoutException>();
    }

    [Fact]
    public async Task CancellationByTheCaller_IsNotReportedAsUnhealthy()
    {
        using var cancellation = new CancellationTokenSource();
        var (check, logger) = Create(() => new TaskCompletionSource().Task);
        await cancellation.CancelAsync();

        var act = () => check.CheckHealthAsync(new HealthCheckContext(), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        logger.Entries.Should().BeEmpty();
    }
}
