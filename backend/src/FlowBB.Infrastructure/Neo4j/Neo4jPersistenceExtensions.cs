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
        services.AddSingleton<IDriver>(provider =>
        {
            var options = provider.GetRequiredService<Neo4jOptions>();
            options.Validate();
            return GraphDatabase.Driver(
                options.Uri,
                AuthTokens.Basic(options.Username, options.Password));
        });

        services.AddScoped<Neo4jAttendanceRepository>();
        services.AddScoped<IAttendanceRepository>(provider =>
            provider.GetRequiredService<Neo4jAttendanceRepository>());
        services.AddScoped<IAttendanceOriginLookup>(provider =>
            provider.GetRequiredService<Neo4jAttendanceRepository>());
        services.AddNeo4jPulseDataReader();
        services.AddScoped<IEventRepository, Neo4jEventRepository>();

        return services;
    }
}
