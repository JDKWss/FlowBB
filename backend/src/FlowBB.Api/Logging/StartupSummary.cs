using FlowBB.Infrastructure.Neo4j;

namespace FlowBB.Api.Logging;

/// <summary>
/// Log startowy z bezpiecznym podsumowaniem konfiguracji: srodowisko, host i nazwa bazy Neo4j oraz dozwolone origin CORS.
/// Nigdy nie zawiera uzytkownika, hasla ani calego adresu polaczenia (adres moze niesc dane logowania).
/// </summary>
public static class StartupSummary
{
    public const string NotSet = "(not set)";

    public static void Log(ILogger logger, IConfiguration configuration, IHostEnvironment environment)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").GetChildren().Select(origin => origin.Value).OfType<string>().ToArray();

        logger.LogInformation(
            "FlowBB API starting. Environment={Environment}, Neo4jHost={Neo4jHost}, Neo4jDatabase={Neo4jDatabase}, CorsOrigins={CorsOrigins}",
            environment.EnvironmentName,
            DescribeNeo4jHost(configuration[Neo4jOptions.UriVariable]),
            string.IsNullOrWhiteSpace(configuration[Neo4jOptions.DatabaseVariable]) ? NotSet : configuration[Neo4jOptions.DatabaseVariable],
            origins);
    }

    /// <summary>Zwraca wylacznie nazwe hosta (bez schematu, portu i danych logowania z adresu).</summary>
    public static string DescribeNeo4jHost(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return NotSet;
        }

        return Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && !string.IsNullOrEmpty(parsed.Host)
            ? parsed.Host
            : "(invalid)";
    }
}
