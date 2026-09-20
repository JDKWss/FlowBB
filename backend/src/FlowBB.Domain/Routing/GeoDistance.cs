using FlowBB.Domain.Common;

namespace FlowBB.Domain.Routing;

public static class GeoDistance
{
    private const double EarthRadiusKilometers = 6371.0088;

    /// <summary>Odleglosc po lini prostej (wzor haversine) w kilometrach.</summary>
    public static double KilometersBetween(GeoPoint first, GeoPoint second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var latitudeDelta = ToRadians(second.Latitude - first.Latitude);
        var longitudeDelta = ToRadians(second.Longitude - first.Longitude);
        var a = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
                + Math.Cos(ToRadians(first.Latitude)) * Math.Cos(ToRadians(second.Latitude))
                * Math.Pow(Math.Sin(longitudeDelta / 2), 2);

        return 2 * EarthRadiusKilometers * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
