using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Infrastructure.Neo4j;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace FlowBB.Infrastructure.Tests.Neo4j;

public sealed class Neo4jPersistenceRegistrationTests
{
    [Theory]
    [InlineData(typeof(IAttendanceRepository), typeof(Neo4jAttendanceRepository))]
    [InlineData(typeof(IAttendanceOriginLookup), typeof(Neo4jAttendanceOriginLookup))]
    [InlineData(typeof(IEventRepository), typeof(Neo4jEventRepository))]
    [InlineData(typeof(IPulseDataReader), typeof(Neo4jPulseDataReader))]
    [InlineData(typeof(ICrewRepository), typeof(Neo4jCrewRepository))]
    public void AddNeo4jPersistence_RegistersEveryPortWithItsNeo4jAdapter(Type port, Type adapter)
    {
        var services = new ServiceCollection();

        services.AddNeo4jPersistence();

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == port &&
            descriptor.ImplementationType == adapter &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }
}
