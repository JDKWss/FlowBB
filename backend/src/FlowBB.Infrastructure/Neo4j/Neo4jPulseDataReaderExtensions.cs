using FlowBB.Application.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace FlowBB.Infrastructure.Neo4j;

public static class Neo4jPulseDataReaderExtensions
{
    public static IServiceCollection AddNeo4jPulseDataReader(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IPulseDataReader, Neo4jPulseDataReader>();
        return services;
    }
}
