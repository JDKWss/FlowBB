using Microsoft.Extensions.Diagnostics.HealthChecks;
using Neo4j.Driver;

namespace FlowBB.Api.Health;

/// <summary>
/// Sprawdza gotowosc API: czy da sie polaczyc z Neo4j. To osobna sprawa niz zywotnosc procesu (<c>/health</c>).
/// Wynik dla klienta jest celowo ogolny: bez hosta, komunikatu wyjatku i danych logowania. Szczegoly trafiaja tylko do logu.
/// </summary>
public sealed class Neo4jReadinessCheck(
    IDriver driver,
    ILogger<Neo4jReadinessCheck> logger,
    TimeSpan? timeout = null) : IHealthCheck
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    public const string ReadyDescription = "Neo4j is reachable.";
    public const string NotReadyDescription = "Neo4j is not reachable.";

    private readonly TimeSpan _timeout = timeout ?? DefaultTimeout;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await driver.VerifyConnectivityAsync().WaitAsync(_timeout, cancellationToken);
            return HealthCheckResult.Healthy(ReadyDescription);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Neo4j readiness check failed (timeout {TimeoutSeconds} s).", _timeout.TotalSeconds);
            return HealthCheckResult.Unhealthy(NotReadyDescription);
        }
    }
}
