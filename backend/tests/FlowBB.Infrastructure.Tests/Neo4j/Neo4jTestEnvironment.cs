using FlowBB.Infrastructure.Neo4j;

namespace FlowBB.Infrastructure.Tests.Neo4j;

/// <summary>
/// Osobne zmienne od <c>NEO4J_*</c>, zeby test nigdy nie trafil przypadkiem w baze aplikacji (np. Aura z .env).
/// Testy zapisuja i usuwaja dane, wiec wymagaja jawnego potwierdzenia jednorazowej instancji.
/// </summary>
public static class Neo4jTestEnvironment
{
    public const string UriVariable = "FLOWBB_NEO4J_TEST_URI";
    public const string DatabaseVariable = "FLOWBB_NEO4J_TEST_DATABASE";
    public const string UsernameVariable = "FLOWBB_NEO4J_TEST_USERNAME";
    public const string PasswordVariable = "FLOWBB_NEO4J_TEST_PASSWORD";
    public const string ConfirmDisposableVariable = "FLOWBB_NEO4J_TEST_CONFIRM_DISPOSABLE";

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(UriVariable)) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PasswordVariable));

    public static Neo4jOptions Load()
    {
        var options = new Neo4jOptions(
            Environment.GetEnvironmentVariable(UriVariable)!,
            Environment.GetEnvironmentVariable(DatabaseVariable) ?? "neo4j",
            Environment.GetEnvironmentVariable(UsernameVariable) ?? "neo4j",
            Environment.GetEnvironmentVariable(PasswordVariable)!);

        Neo4jTestSafetyGuard.EnsureDisposable(
            options.Uri,
            Environment.GetEnvironmentVariable(ConfirmDisposableVariable));

        return options;
    }
}
