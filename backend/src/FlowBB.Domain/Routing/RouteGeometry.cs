namespace FlowBB.Domain.Routing;

public sealed record RouteCoordinate
{
    public RouteCoordinate(double longitude, double latitude)
    {
        if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be within [-180, 180].");
        }

        if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be within [-90, 90].");
        }

        Longitude = longitude;
        Latitude = latitude;
    }

    public double Longitude { get; }

    public double Latitude { get; }
}

/// <summary>Provider-neutral GeoJSON LineString geometry in longitude/latitude order.</summary>
public sealed record RouteGeometry
{
    public RouteGeometry(IReadOnlyList<RouteCoordinate> coordinates)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        if (coordinates.Count < 2)
        {
            throw new ArgumentException("A route LineString needs at least two coordinates.", nameof(coordinates));
        }

        if (coordinates.Any(coordinate => coordinate is null))
        {
            throw new ArgumentException("Route coordinates cannot contain null values.", nameof(coordinates));
        }

        Coordinates = coordinates.ToArray();
    }

    public string Type => "LineString";

    public IReadOnlyList<RouteCoordinate> Coordinates { get; }
}
