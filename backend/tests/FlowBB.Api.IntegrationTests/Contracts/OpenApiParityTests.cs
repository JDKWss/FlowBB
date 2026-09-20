using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FlowBB.Api.IntegrationTests.Contracts;

/// <summary>
/// Pilnuje, aby operacje w <c>contracts/openapi.yaml</c> (operationId, sciezka, metoda) odpowiadaly endpointom, ktore host
/// faktycznie mapuje. Ksztalty odpowiedzi sprawdzaja testy endpointow; tu tylko spis operacji.
/// </summary>
public sealed partial class OpenApiParityTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Operation(string Path, string Method);

    [GeneratedRegex(@"^  (/\S+):\s*$")]
    private static partial Regex PathLine();

    [GeneratedRegex(@"^    (get|post|put|patch|delete):\s*$")]
    private static partial Regex MethodLine();

    [GeneratedRegex(@"^      operationId:\s*(\S+)\s*$")]
    private static partial Regex OperationIdLine();

    private static string FindContract()
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

    private static Dictionary<string, Operation> ReadContractOperations()
    {
        var operations = new Dictionary<string, Operation>();
        string? path = null;
        string? method = null;
        foreach (var line in File.ReadLines(FindContract()).SkipWhile(line => line != "paths:").TakeWhile(line => line != "components:"))
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
                operations.Add(idMatch.Groups[1].Value, new Operation(path, method));
            }
        }

        return operations;
    }

    private Dictionary<string, (Operation Operation, bool HasMethod)> ReadRuntimeOperations() => factory.Services
        .GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
        .Where(endpoint => endpoint.RoutePattern.RawText is { } text && (text.StartsWith("/api/") || text.StartsWith("/health")))
        .Select(endpoint => (
            Name: endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName,
            Path: endpoint.RoutePattern.RawText!.TrimEnd('/'),
            Methods: endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods))
        .Where(item => item.Name is not null)
        .ToDictionary(
            item => item.Name!,
            item => (new Operation(item.Path, item.Methods?.Single() ?? "GET"), item.Methods is not null));

    [Fact]
    public void OperationIds_MatchTheMappedEndpoints()
    {
        var contract = ReadContractOperations().Keys;
        var runtime = ReadRuntimeOperations().Keys;

        contract.Should().BeEquivalentTo(runtime);
    }

    [Fact]
    public void PathsAndMethods_MatchTheMappedEndpoints()
    {
        var contract = ReadContractOperations();
        var runtime = ReadRuntimeOperations();

        foreach (var (operationId, expected) in contract)
        {
            var actual = runtime[operationId];
            actual.Operation.Path.Should().Be(expected.Path, "path of {0}", operationId);
            if (actual.HasMethod)
            {
                actual.Operation.Method.Should().Be(expected.Method, "method of {0}", operationId);
            }
        }
    }
}
