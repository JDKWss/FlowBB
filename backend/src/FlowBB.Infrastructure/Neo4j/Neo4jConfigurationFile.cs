namespace FlowBB.Infrastructure.Neo4j;

internal static class Neo4jConfigurationFile
{
    private const string FileName = "Neo4j-46e86353-Created-2026-09-19.txt";

    public static IReadOnlyDictionary<string, string> Load()
    {
        var path = FindPath();
        return path is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : Parse(path);
    }

    private static string? FindPath()
    {
        var startDirectories = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var startDirectory in startDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = FindFrom(startDirectory);
            if (path is not null)
            {
                return path;
            }
        }

        return null;
    }

    private static string? FindFrom(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            var directPath = Path.Combine(directory.FullName, FileName);
            if (File.Exists(directPath))
            {
                return directPath;
            }

            var backendPath = Path.Combine(directory.FullName, "backend", FileName);
            if (File.Exists(backendPath))
            {
                return backendPath;
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> Parse(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in File.ReadLines(path))
        {
            AddLine(values, line);
        }

        return values;
    }

    private static void AddLine(IDictionary<string, string> values, string line)
    {
        var trimmedLine = line.Trim();
        if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#'))
        {
            return;
        }

        var separatorIndex = trimmedLine.IndexOf('=');
        if (separatorIndex <= 0)
        {
            return;
        }

        var key = trimmedLine[..separatorIndex].Trim();
        var value = trimmedLine[(separatorIndex + 1)..].Trim();
        values[key] = value;
    }
}
