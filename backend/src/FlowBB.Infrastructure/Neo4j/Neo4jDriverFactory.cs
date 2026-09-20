using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

public static class Neo4jDriverFactory
{
    private static readonly TimeSpan DriverTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Tworzy wspoldzielony <see cref="IDriver"/>. Ma zyc tak dlugo jak aplikacja (singleton), bo trzyma pule polaczen.
    /// </summary>
    public static IDriver Create(Neo4jOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return GraphDatabase.Driver(
            options.Uri,
            AuthTokens.Basic(options.Username, options.Password),
            config => config
                .WithConnectionTimeout(DriverTimeout)
                .WithConnectionAcquisitionTimeout(DriverTimeout)
                .WithMaxTransactionRetryTime(DriverTimeout));
    }
}
