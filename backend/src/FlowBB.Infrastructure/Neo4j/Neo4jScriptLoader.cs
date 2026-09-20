using System.Reflection;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

internal static class Neo4jScriptLoader
{
    public static IReadOnlyList<string> LoadStatements(string resourceName, string? endMarker = null)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded Neo4j script {resourceName} was not found.");
        using var reader = new StreamReader(stream);
        var script = TrimAtMarker(reader.ReadToEnd(), resourceName, endMarker);
        var executableScript = string.Join(
            '\n',
            script.ReplaceLineEndings("\n")
                .Split('\n')
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        return executableScript
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    public static async Task ApplyInTransactionAsync(
        IDriver driver,
        string database,
        IReadOnlyList<string> statements)
    {
        await using var session = driver.AsyncSession(config => config.WithDatabase(database));
        await session.ExecuteWriteAsync(async transaction =>
        {
            foreach (var statement in statements)
            {
                var cursor = await transaction.RunAsync(statement);
                await cursor.ConsumeAsync();
            }
        });
    }

    private static string TrimAtMarker(string script, string resourceName, string? endMarker)
    {
        if (endMarker is null)
        {
            return script;
        }

        var markerIndex = script.IndexOf(endMarker, StringComparison.Ordinal);
        return markerIndex < 0
            ? throw new InvalidOperationException($"End marker {endMarker} was not found in {resourceName}.")
            : script[..markerIndex];
    }
}
