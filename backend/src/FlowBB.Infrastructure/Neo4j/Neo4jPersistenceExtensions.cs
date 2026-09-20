using FlowBB.Application.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

public static class Neo4jPersistenceExtensions
{
    public static IServiceCollection AddNeo4jPersistence(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(_ => Neo4jOptions.FromEnvironment());
        services.AddSingleton<IDriver>(provider => Neo4jDriverFactory.Create(provider.GetRequiredService<Neo4jOptions>()));

        services.AddScoped<IAttendanceRepository, Neo4jAttendanceRepository>();
        services.AddScoped<IAttendanceOriginLookup, Neo4jAttendanceOriginLookup>();
        services.AddNeo4jPulseDataReader();
        services.AddScoped<IEventRepository, Neo4jEventRepository>();
        services.AddScoped<ICrewRepository, Neo4jCrewRepository>();

        return services;
    }
}
