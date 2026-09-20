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
public sealed class OpenApiParityTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private Dictionary<string, (ContractOperation Operation, bool HasMethod)> ReadRuntimeOperations() => factory.Services
        .GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
        .Where(endpoint => endpoint.RoutePattern.RawText is { } text && (text.StartsWith("/api/") || text.StartsWith("/health")))
        .Select(endpoint => (
            Name: endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName,
            Path: endpoint.RoutePattern.RawText!.TrimEnd('/'),
            Methods: endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods))
        .Where(item => item.Name is not null)
        .ToDictionary(
            item => item.Name!,
            item => (new ContractOperation(item.Path, item.Methods?.Single() ?? "GET"), item.Methods is not null));

    [Fact]
    public void OperationIds_MatchTheMappedEndpoints()
    {
        var contract = OpenApiContract.ReadOperations().Keys;
        var runtime = ReadRuntimeOperations().Keys;

        contract.Should().BeEquivalentTo(runtime);
    }

    [Fact]
    public void PathsAndMethods_MatchTheMappedEndpoints()
    {
        var contract = OpenApiContract.ReadOperations();
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
