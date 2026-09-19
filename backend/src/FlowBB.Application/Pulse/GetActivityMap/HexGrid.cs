namespace FlowBB.Application.Pulse.GetActivityMap;

public readonly record struct HexCoordinate(int Q, int R)
{
    public string Id => $"hex-{Q}-{R}";
}

public readonly record struct HexVertex(double Latitude, double Longitude);

/// <summary>
/// Siatka heksagonow (pointy-top, wspolrzedne osiowe) liczona w lokalnym ukladzie metrycznym
/// (rzut rownoodlegly wokol stalego punktu odniesienia w Bielsku-Bialej), a wynik zwracany w
/// stopniach EPSG:4326. Dla skali miasta blad rzutu jest pomijalny wobec rozmiaru komorki.
/// </summary>
public static class HexGrid
{
    public const double DefaultCellWidthMeters = 900;

    private const double EarthRadiusMeters = 6_371_008.8;
    private const double OriginLatitude = 49.8225;
    private const double OriginLongitude = 19.0444;
    private const double DegreesToRadians = Math.PI / 180;

    private static readonly double MetersPerDegreeLatitude = EarthRadiusMeters * DegreesToRadians;
    private static readonly double MetersPerDegreeLongitude =
        MetersPerDegreeLatitude * Math.Cos(OriginLatitude * DegreesToRadians);
    private static readonly double Sqrt3 = Math.Sqrt(3);

    public static HexCoordinate CellAt(double latitude, double longitude, double cellWidthMeters = DefaultCellWidthMeters)
    {
        var size = SizeFor(cellWidthMeters);
        var x = (longitude - OriginLongitude) * MetersPerDegreeLongitude;
        var y = (latitude - OriginLatitude) * MetersPerDegreeLatitude;

        var q = (Sqrt3 / 3 * x - y / 3) / size;
        var r = 2.0 / 3 * y / size;
        return Round(q, r);
    }

    public static IReadOnlyList<HexVertex> VerticesOf(HexCoordinate cell, double cellWidthMeters = DefaultCellWidthMeters)
    {
        var size = SizeFor(cellWidthMeters);
        var centerX = size * Sqrt3 * (cell.Q + cell.R / 2.0);
        var centerY = size * 1.5 * cell.R;

        var vertices = new HexVertex[6];
        for (var i = 0; i < vertices.Length; i++)
        {
            var angle = (60.0 * i - 30) * DegreesToRadians;
            vertices[i] = ToGeographic(centerX + size * Math.Cos(angle), centerY + size * Math.Sin(angle));
        }

        return vertices;
    }

    private static double SizeFor(double cellWidthMeters)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cellWidthMeters, 0, nameof(cellWidthMeters));
        return cellWidthMeters / Sqrt3;
    }

    private static HexVertex ToGeographic(double xMeters, double yMeters) =>
        new(OriginLatitude + yMeters / MetersPerDegreeLatitude,
            OriginLongitude + xMeters / MetersPerDegreeLongitude);

    private static HexCoordinate Round(double q, double r)
    {
        var s = -q - r;
        var roundedQ = Math.Round(q);
        var roundedR = Math.Round(r);
        var roundedS = Math.Round(s);

        var deltaQ = Math.Abs(roundedQ - q);
        var deltaR = Math.Abs(roundedR - r);
        var deltaS = Math.Abs(roundedS - s);

        if (deltaQ > deltaR && deltaQ > deltaS)
        {
            roundedQ = -roundedR - roundedS;
        }
        else if (deltaR > deltaS)
        {
            roundedR = -roundedQ - roundedS;
        }

        return new HexCoordinate((int)roundedQ, (int)roundedR);
    }
}
