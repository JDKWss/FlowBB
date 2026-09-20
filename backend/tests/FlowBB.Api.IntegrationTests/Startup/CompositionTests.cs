using System.Text.RegularExpressions;
using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Abstractions.Realtime;
using FlowBB.Application.Abstractions.Routing;
using FlowBB.Application.Attendance.DeleteAttendance;
using FlowBB.Application.Attendance.UpsertAttendance;
using FlowBB.Application.Crews.GetEventGroups;
using FlowBB.Application.Crews.JoinCrew;
using FlowBB.Application.Crews.LeaveCrew;
using FlowBB.Application.Events.GetEvent;
using FlowBB.Application.Events.GetEvents;
using FlowBB.Application.Pulse.GetActivityMap;
using FlowBB.Application.Pulse.GetEventPulse;
using FlowBB.Application.Pulse.GetPulseHexagons;
using FlowBB.Application.Pulse.GetPulseSummary;
using FlowBB.Application.Routing.GetEventRoute;
using FlowBB.Infrastructure.Neo4j;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FlowBB.Api.IntegrationTests.Startup;

[Collection(CompositionTestCollection.Name)]
public sealed class CompositionTests
{
    private const string OperationIdPattern = @"operationId:\s*(\w+)";

    private static readonly Type[] MappedHandlerTypes =
    [
        typeof(GetEventsHandler),
        typeof(GetEventHandler),
        typeof(UpsertAttendanceHandler),
        typeof(DeleteAttendanceHandler),
        typeof(GetActivityMapHandler),
        typeof(GetPulseHexagonsHandler),
        typeof(GetEventPulseHandler),
        typeof(GetPulseSummaryHandler),
        typeof(GetEventRouteHandler),
        typeof(GetEventGroupsHandler),
        typeof(JoinCrewHandler),
        typeof(LeaveCrewHandler)
    ];

    private static readonly IReadOnlyDictionary<string, string> FakeNeo4jEnvironment =
        new Dictionary<string, string>
        {
            [Neo4jOptions.UriVariable] = "neo4j://localhost:7687",
            [Neo4jOptions.DatabaseVariable] = "flowbb-test",
            [Neo4jOptions.UsernameVariable] = "test-user",
            [Neo4jOptions.PasswordVariable] = "test-password"
        };

    [Fact]
    public Task OpenApiOperations_AreMapped() => WithNeo4jEnvironmentAsync(() =>
    {
        using var factory = new ValidatedFactory();
        var mappedOperations = GetMappedOperations(factory.Services);
        var contractOperations = GetContractOperations();

        contractOperations.Should().NotBeEmpty("the contract must be parsed, otherwise the check below is vacuous");
        contractOperations.Except(mappedOperations).Should().BeEmpty("every operationId from the contract must be mapped");

        return Task.CompletedTask;
    });

    [Fact]
    public Task EveryPort_HasExactlyOneImplementation() => WithNeo4jEnvironmentAsync(() =>
    {
        using var factory = new ValidatedFactory();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        AssertSingle<IEventRepository>(services);
        AssertSingle<IEventLookup>(services);
        AssertSingle<IAttendanceRepository>(services);
        AssertSingle<IAttendanceOriginLookup>(services);
        AssertSingle<ICrewRepository>(services);
        AssertSingle<IPulseDataReader>(services);
        AssertSingle<IPulseNotifier>(services);
        AssertSingle<IRoutePlanner>(services);
        ResolveMappedHandlers(services);

        return Task.CompletedTask;
    });

    [Fact]
    public async Task DevelopmentHost_PassesServiceProviderValidation()
    {
        var action = () => WithNeo4jEnvironmentAsync(async () =>
        {
            using var factory = new ValidatedFactory();
            using var client = factory.CreateClient();
            using var response = await client.GetAsync("/health");
            response.EnsureSuccessStatusCode();
        });

        await action.Should().NotThrowAsync();
    }

    private static HashSet<string> GetMappedOperations(IServiceProvider services) =>
        services.GetRequiredService<EndpointDataSource>().Endpoints
            .Select(endpoint => endpoint.Metadata.GetMetadata<EndpointNameMetadata>()?.EndpointName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> GetContractOperations()
    {
        var contract = File.ReadAllText(FindOpenApiContract());
        return Regex.Matches(
                contract,
                OperationIdPattern,
                RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string FindOpenApiContract()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "contracts", "openapi.yaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Could not locate contracts/openapi.yaml.");
    }

    private static void AssertSingle<T>(IServiceProvider services)
        where T : class => services.GetServices<T>().Should().ContainSingle();

    private static void ResolveMappedHandlers(IServiceProvider services)
    {
        foreach (var handlerType in MappedHandlerTypes)
        {
            services.GetRequiredService(handlerType).Should().NotBeNull();
        }
    }

    private static async Task WithNeo4jEnvironmentAsync(Func<Task> action)
    {
        var originalValues = FakeNeo4jEnvironment.Keys.ToDictionary(
            variable => variable,
            Environment.GetEnvironmentVariable);

        try
        {
            foreach (var variable in FakeNeo4jEnvironment)
            {
                Environment.SetEnvironmentVariable(variable.Key, variable.Value);
            }

            await action();
        }
        finally
        {
            foreach (var variable in originalValues)
            {
                Environment.SetEnvironmentVariable(variable.Key, variable.Value);
            }
        }
    }

    private sealed class ValidatedFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseDefaultServiceProvider(options =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            });
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CompositionTestCollection
{
    public const string Name = "Composition tests with process environment";
}
