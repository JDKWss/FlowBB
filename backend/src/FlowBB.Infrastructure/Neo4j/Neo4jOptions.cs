namespace FlowBB.Infrastructure.Neo4j;

public sealed record Neo4jOptions(
    string Uri,
    string Database,
    string Username,
    string Password)
{
    public const string UriVariable = "NEO4J_URI";
    public const string DatabaseVariable = "NEO4J_DATABASE";
    public const string UsernameVariable = "NEO4J_USERNAME";
    public const string PasswordVariable = "NEO4J_PASSWORD";

    public static Neo4jOptions FromEnvironment()
    {
        return new Neo4jOptions(
            Environment.GetEnvironmentVariable(UriVariable) ?? "neo4j://127.0.0.1:7687",
            Environment.GetEnvironmentVariable(DatabaseVariable) ?? "flowbb",
            GetRequiredVariable(UsernameVariable),
            GetRequiredVariable(PasswordVariable));
    }

    public void Validate()
    {
        if (!System.Uri.TryCreate(Uri, UriKind.Absolute, out var parsedUri) ||
            parsedUri.Scheme is not ("neo4j" or "neo4j+s" or "neo4j+ssc" or "bolt" or "bolt+s" or "bolt+ssc"))
        {
            throw new InvalidOperationException("Neo4j URI is invalid or uses an unsupported scheme.");
        }

        EnsureNotBlank(Database, nameof(Database));
        EnsureNotBlank(Username, nameof(Username));
        EnsureNotBlank(Password, nameof(Password));
    }

    private static string GetRequiredVariable(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Required environment variable {variableName} is not set.");
    }

    private static void EnsureNotBlank(string value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Neo4j option {propertyName} cannot be blank.");
        }
    }
}
