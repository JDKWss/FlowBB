using FlowBB.Api.Endpoints.Routing;
using FlowBB.Application.Abstractions.Routing;
using FlowBB.Infrastructure.Routing;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowBB.Api.IntegrationTests.Routing;

public sealed class RoutingModeTests
{
    private static IConfiguration Config(string? mode) => new ConfigurationBuilder()
        .AddInMemoryCollection(mode is null ? [] : [new KeyValuePair<string, string?>(RoutingModeConfiguration.Key, mode)])
        .Build();

    private static ServiceProvider Provider(RoutingMode mode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new RoutingServiceOptions(new Uri("http://routing.invalid:8000"), TimeSpan.FromSeconds(3), true));
        services.AddHttpClient<RoutingServiceClient>();
        services.AddRoutingModule(mode);
        return services.BuildServiceProvider(validateScopes: true);
    }

    [Theory]
    [InlineData(null, RoutingMode.Demo)]
    [InlineData("", RoutingMode.Demo)]
    [InlineData("  ", RoutingMode.Demo)]
    [InlineData("Demo", RoutingMode.Demo)]
    [InlineData("demo", RoutingMode.Demo)]
    [InlineData("RoadRouting", RoutingMode.RoadRouting)]
    [InlineData("roadrouting", RoutingMode.RoadRouting)]
    public void GetRoutingMode_ParsesConfiguration(string? value, RoutingMode expected)
    {
        Config(value).GetRoutingMode().Should().Be(expected);
    }

    [Theory]
    [InlineData("Real")]
    [InlineData("1")]
    [InlineData("Demo,RoadRouting")]
    public void GetRoutingMode_WithUnknownValue_FailsStartup(string value)
    {
        var act = () => Config(value).GetRoutingMode();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Routing:Mode*Demo*RoadRouting*");
    }

    [Fact]
    public void DemoMode_UsesTheTimetablePlannerWithDemoFallback()
    {
        using var provider = Provider(RoutingMode.Demo);

        // Domyslny tryb nie uzywa CompositeRoutePlanner, wiec to jest jedyne miejsce, w ktorym planer z rozkladu
        // MZK trafia do aplikacji. Surowy DemoRoutePlanner oznaczalby, ze rozklad jest martwym kodem.
        provider.GetRequiredService<IRoutePlanner>().Should().BeOfType<TimetableFallbackRoutePlanner>();
    }

    [Fact]
    public void RoadRoutingMode_UsesCompositePlannerWithFallback()
    {
        using var provider = Provider(RoutingMode.RoadRouting);

        provider.GetRequiredService<IRoutePlanner>().Should().BeOfType<CompositeRoutePlanner>();
    }

    [Fact]
    public void ParameterlessModule_KeepsCompositePlanner()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new RoutingServiceOptions(new Uri("http://routing.invalid:8000"), TimeSpan.FromSeconds(3), true));
        services.AddHttpClient<RoutingServiceClient>();
        services.AddRoutingModule();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IRoutePlanner>().Should().BeOfType<CompositeRoutePlanner>();
    }
}
