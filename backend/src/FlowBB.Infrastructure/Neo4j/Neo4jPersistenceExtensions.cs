using FlowBB.Application.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

public static class Neo4jPersistenceExtensions
{
    /// <summary>
    /// Limit oczekiwania na polaczenie, wolne polaczenie z puli i ponowienia transakcji. Domyslne ustawienia sterownika
    /// powodowaly, ze przy niedostepnej bazie zadanie wisialo ok. 37 s zanim zwrocilo blad.
    /// </summary>
    public static readonly TimeSpan DriverTimeout = TimeSpan.FromSeconds(5);

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
                AuthTokens.Basic(options.Username, options.Password),
                config => config
                    .WithConnectionTimeout(DriverTimeout)
                    .WithConnectionAcquisitionTimeout(DriverTimeout)
                    .WithMaxTransactionRetryTime(DriverTimeout));
        });

        services.AddScoped<IAttendanceRepository, Neo4jAttendanceRepository>();
        services.AddScoped<IAttendanceOriginLookup, Neo4jAttendanceOriginLookup>();
        services.AddNeo4jPulseDataReader();
        services.AddScoped<IEventRepository, Neo4jEventRepository>();
        services.AddScoped<IEventWriter, Neo4jEventWriter>();
        services.AddScoped<ICrewRepository, Neo4jCrewRepository>();

        return services;
    }
}
