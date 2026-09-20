namespace FlowBB.Infrastructure.Tests.Neo4j;

internal static class Neo4jTestSafetyGuard
{
    private const string AuraHostSuffix = ".databases.neo4j.io";

    public static void EnsureDisposable(string configuredUri, string? confirmation)
    {
        if (!Uri.TryCreate(configuredUri, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException(
                $"{Neo4jTestEnvironment.UriVariable} musi zawierac poprawny bezwzgledny adres Neo4j.");
        }

        if (IsAuraHost(uri.Host))
        {
            throw new InvalidOperationException(
                "Testy integracyjne Neo4j nie moga dzialac na hostach Aura (*.databases.neo4j.io). " +
                "Uzyj jednorazowej lokalnej instancji.");
        }

        if (!string.Equals(confirmation?.Trim(), "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Testy integracyjne Neo4j zapisuja i usuwaja dane. " +
                $"Ustaw {Neo4jTestEnvironment.ConfirmDisposableVariable}=true tylko dla jednorazowej bazy testowej.");
        }
    }

    private static bool IsAuraHost(string host)
    {
        return host.Equals("databases.neo4j.io", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(AuraHostSuffix, StringComparison.OrdinalIgnoreCase);
    }
}
