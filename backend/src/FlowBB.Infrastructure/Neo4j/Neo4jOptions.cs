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
        var fileValues = Neo4jConfigurationFile.Load();

        return new Neo4jOptions(
            GetValueOrDefault(UriVariable, fileValues, "neo4j://127.0.0.1:7687"),
            GetValueOrDefault(DatabaseVariable, fileValues, "flowbb"),
            GetRequiredValue(UsernameVariable, fileValues),
            GetRequiredValue(PasswordVariable, fileValues));
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

    private static string GetRequiredValue(
        string variableName,
        IReadOnlyDictionary<string, string> fileValues)
    {
        var value = GetValue(variableName, fileValues);
        return value ?? throw new InvalidOperationException(
            $"Required Neo4j setting {variableName} is missing from the environment and configuration file.");
    }

    private static string? GetValue(
        string variableName,
        IReadOnlyDictionary<string, string> fileValues)
    {
        var environmentValue = Environment.GetEnvironmentVariable(variableName);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return environmentValue;
        }

        return fileValues.TryGetValue(variableName, out var fileValue) &&
               !string.IsNullOrWhiteSpace(fileValue)
            ? fileValue
            : null;
    }

    private static string GetValueOrDefault(
        string variableName,
        IReadOnlyDictionary<string, string> fileValues,
        string defaultValue)
    {
        return GetValue(variableName, fileValues) ?? defaultValue;
    }

    private static void EnsureNotBlank(string value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Neo4j option {propertyName} cannot be blank.");
        }
    }
}
