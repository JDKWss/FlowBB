namespace FlowBB.Api.Endpoints.Routing;

/// <summary>
/// Tryb planowania tras (konfiguracja <c>Routing:Mode</c>). <see cref="Demo"/> to domyslny, deterministyczny i
/// dzialajacy bez internetu tryb demonstracyjny: API nie wywoluje uslugi drogowej. <see cref="RoadRouting"/>
/// wlacza prywatna usluge routingu drogowego z kontrolowanym fallbackiem do <c>DemoRoutePlanner</c>.
/// </summary>
public enum RoutingMode
{
    Demo,
    RoadRouting
}

public static class RoutingModeConfiguration
{
    public const string Key = "Routing:Mode";

    /// <summary>Czyta tryb z konfiguracji; brak wartosci to <see cref="RoutingMode.Demo"/>, a nieznana wartosc jest bledem startu.</summary>
    public static RoutingMode GetRoutingMode(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var value = configuration[Key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return RoutingMode.Demo;
        }

        // Tylko nazwy: Enum.TryParse przyjmuje tez liczby ("1") i listy ("Demo,RoadRouting"), co maskowaloby literowki.
        var name = Enum.GetNames<RoutingMode>()
            .FirstOrDefault(candidate => string.Equals(candidate, value.Trim(), StringComparison.OrdinalIgnoreCase));
        return name is null
            ? throw new InvalidOperationException($"{Key} must be one of: {string.Join(", ", Enum.GetNames<RoutingMode>())}.")
            : Enum.Parse<RoutingMode>(name);
    }
}
