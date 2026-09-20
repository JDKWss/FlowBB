using FlowBB.Domain.Common;

namespace FlowBB.Domain.Routing;

/// <summary>
/// Przystanek komunikacji miejskiej na trasie. Wspolrzedne pochodza z publicznej infrastruktury przystankowej (OSM)
/// i moga opuscic backend; punkt startu uzytkownika nie jest przystankiem i nie trafia do tego typu.
/// </summary>
public sealed record RouteStop
{
    public const int NameMaxLength = 120;

    public RouteStop(string name, GeoPoint location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, NameMaxLength, nameof(name));
        ArgumentNullException.ThrowIfNull(location);

        Name = name;
        Location = location;
    }

    public string Name { get; }

    public GeoPoint Location { get; }
}
