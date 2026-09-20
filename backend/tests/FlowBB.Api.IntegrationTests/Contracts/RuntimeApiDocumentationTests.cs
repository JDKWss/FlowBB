using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlowBB.Api.IntegrationTests.Contracts;

/// <summary>
/// Dokument OpenAPI generowany przez host (<c>/openapi/v1.json</c>) oraz Scalar w Development musza odpowiadac kontraktowi,
/// a w Production dokumentacja nie moze byc wystawiona. Uzupelnia <see cref="OpenApiParityTests"/>, ktory porownuje
/// kontrakt z zarejestrowanymi endpointami.
/// </summary>
public sealed class RuntimeApiDocumentationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    // Endpoint health-check (`/health/ready`) nie ma metadanych API explorera, wiec generator go nie opisuje.
    private static readonly string[] NotInGeneratedDocument = ["getReadiness"];

    private HttpClient CreateClient(string environment) => factory
        .WithWebHostBuilder(builder => builder.UseEnvironment(environment))
        .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static Dictionary<string, ContractOperation> ReadGeneratedOperations(JsonElement document)
    {
        var operations = new Dictionary<string, ContractOperation>();
        foreach (var path in document.GetProperty("paths").EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject())
            {
                var operationId = method.Value.GetProperty("operationId").GetString()!;
                operations.Add(operationId, new ContractOperation(path.Name, method.Name.ToUpperInvariant()));
            }
        }

        return operations;
    }

    [Fact]
    public async Task GeneratedDocument_DescribesTheSameOperationsAsTheContract()
    {
        using var client = CreateClient("Development");

        using var response = await client.GetAsync("/openapi/v1.json");
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var generated = ReadGeneratedOperations(document);
        var expected = OpenApiContract.ReadOperations()
            .Where(operation => !NotInGeneratedDocument.Contains(operation.Key))
            .ToDictionary(operation => operation.Key, operation => operation.Value);
        generated.Should().BeEquivalentTo(expected, "the generated document and contracts/openapi.yaml must list the same operations");
    }

    [Fact]
    public async Task Scalar_IsServedInDevelopment()
    {
        using var client = CreateClient("Development");

        using var response = await client.GetAsync("/scalar/v1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    [InlineData("/scalar/v1")]
    public async Task Documentation_IsNotServedInProduction(string path)
    {
        using var client = CreateClient("Production");

        using var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
