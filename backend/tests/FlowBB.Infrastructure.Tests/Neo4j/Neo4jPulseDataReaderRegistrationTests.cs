using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Infrastructure.Neo4j;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace FlowBB.Infrastructure.Tests.Neo4j;

public sealed class Neo4jPulseDataReaderRegistrationTests
{
    [Fact]
    public void AddNeo4jPulseDataReader_RegistersPortAndAdapter()
    {
        var services = new ServiceCollection();

        services.AddNeo4jPulseDataReader();

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IPulseDataReader) &&
            descriptor.ImplementationType == typeof(Neo4jPulseDataReader) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }
}
