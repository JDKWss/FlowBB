using System.Text.RegularExpressions;

namespace FlowBB.Api.IntegrationTests.Contracts;

/// <summary>Operacja z <c>contracts/openapi.yaml</c>: sciezka i metoda HTTP (wielkimi literami).</summary>
internal sealed record ContractOperation(string Path, string Method);

/// <summary>Odczyt spisu operacji z kontraktu bez zewnetrznej biblioteki YAML (prosty wzorzec na strukture pliku).</summary>
internal static partial class OpenApiContract
{
    [GeneratedRegex(@"^  (/\S+):\s*$")]
    private static partial Regex PathLine();

    [GeneratedRegex(@"^    (get|post|put|patch|delete):\s*$")]
    private static partial Regex MethodLine();

    [GeneratedRegex(@"^      operationId:\s*(\S+)\s*$")]
    private static partial Regex OperationIdLine();

    public static string FindPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "contracts", "openapi.yaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("contracts/openapi.yaml was not found above the test output directory.");
    }

    /// <summary>Wszystkie operacje kontraktu, kluczowane przez <c>operationId</c>.</summary>
    public static Dictionary<string, ContractOperation> ReadOperations()
    {
        var operations = new Dictionary<string, ContractOperation>();
        string? path = null;
        string? method = null;
        foreach (var line in File.ReadLines(FindPath()).SkipWhile(line => line != "paths:").TakeWhile(line => line != "components:"))
        {
            if (PathLine().Match(line) is { Success: true } pathMatch)
            {
                (path, method) = (pathMatch.Groups[1].Value, null);
            }
            else if (MethodLine().Match(line) is { Success: true } methodMatch)
            {
                method = methodMatch.Groups[1].Value.ToUpperInvariant();
            }
            else if (OperationIdLine().Match(line) is { Success: true } idMatch && path is not null && method is not null)
            {
                operations.Add(idMatch.Groups[1].Value, new ContractOperation(path, method));
            }
        }

        return operations;
    }
}
