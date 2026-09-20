using FlowBB.Infrastructure.Neo4j;

namespace FlowBB.Infrastructure.Tests.Neo4j;

/// <summary>
/// Osobne zmienne od <c>NEO4J_*</c>, zeby test nigdy nie trafil przypadkiem w baze aplikacji (np. Aura z .env).
/// Testy zapisuja i usuwaja dane, wiec wskazuj wylacznie jednorazowa instancje.
/// </summary>
public static class Neo4jTestEnvironment
{
    public const string UriVariable = "FLOWBB_NEO4J_TEST_URI";
    public const string DatabaseVariable = "FLOWBB_NEO4J_TEST_DATABASE";
    public const string UsernameVariable = "FLOWBB_NEO4J_TEST_USERNAME";
    public const string PasswordVariable = "FLOWBB_NEO4J_TEST_PASSWORD";

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(UriVariable)) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PasswordVariable));

    public static Neo4jOptions Load()
    {
        return new Neo4jOptions(
            Environment.GetEnvironmentVariable(UriVariable)!,
            Environment.GetEnvironmentVariable(DatabaseVariable) ?? "neo4j",
            Environment.GetEnvironmentVariable(UsernameVariable) ?? "neo4j",
            Environment.GetEnvironmentVariable(PasswordVariable)!);
    }
}
