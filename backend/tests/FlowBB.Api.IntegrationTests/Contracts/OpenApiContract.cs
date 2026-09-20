using System.Text.RegularExpressions;

namespace FlowBB.Api.IntegrationTests.Contracts;

/// <summary>Operacja z <c>contracts/openapi.yaml</c>: sciezka i metoda HTTP (wielkimi literami).</summary>
internal sealed record ContractOperation(string Path, string Method);

/// <summary>Odczyt spisu operacji z kontraktu bez zewnetrznej biblioteki YAML (prosty wzorzec na strukture pliku).</summary>
internal static partial class OpenApiContract
{
    private sealed record ParsedOperation(string OperationId, ContractOperation Operation, bool IsPlanned);

    [GeneratedRegex(@"^  (/\S+):\s*$")]
    private static partial Regex PathLine();

    [GeneratedRegex(@"^    (get|post|put|patch|delete):\s*$")]
    private static partial Regex MethodLine();

    [GeneratedRegex(@"^      operationId:\s*(\S+)\s*$")]
    private static partial Regex OperationIdLine();

    [GeneratedRegex(@"^      x-runtime-status:\s*planned\s*$")]
    private static partial Regex PlannedRuntimeLine();

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

    /// <summary>Operacje wymagajace mapowania runtime, kluczowane przez <c>operationId</c>.</summary>
    public static Dictionary<string, ContractOperation> ReadOperations() => ReadAllOperations()
        .Where(operation => !operation.IsPlanned)
        .ToDictionary(operation => operation.OperationId, operation => operation.Operation);

    /// <summary>Operacje contract-first, ktore nie maja jeszcze mapowania runtime.</summary>
    public static Dictionary<string, ContractOperation> ReadPlannedOperations() => ReadAllOperations()
        .Where(operation => operation.IsPlanned)
        .ToDictionary(operation => operation.OperationId, operation => operation.Operation);

    private static IReadOnlyList<ParsedOperation> ReadAllOperations()
    {
        var operations = new List<ParsedOperation>();
        string? path = null;
        string? method = null;
        var isPlanned = false;
        foreach (var line in File.ReadLines(FindPath()).SkipWhile(line => line != "paths:").TakeWhile(line => line != "components:"))
        {
            if (PathLine().Match(line) is { Success: true } pathMatch)
            {
                (path, method) = (pathMatch.Groups[1].Value, null);
                isPlanned = false;
            }
            else if (MethodLine().Match(line) is { Success: true } methodMatch)
            {
                method = methodMatch.Groups[1].Value.ToUpperInvariant();
                isPlanned = false;
            }
            else if (PlannedRuntimeLine().IsMatch(line))
            {
                isPlanned = true;
            }
            else if (OperationIdLine().Match(line) is { Success: true } idMatch && path is not null && method is not null)
            {
                operations.Add(new ParsedOperation(
                    idMatch.Groups[1].Value,
                    new ContractOperation(path, method),
                    isPlanned));
            }
        }

        return operations;
    }
}
