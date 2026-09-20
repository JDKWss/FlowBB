namespace FlowBB.Application.AirQuality;

public sealed record AirQualityStation
{
    public AirQualityStation(string name, double distanceMeters)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Station name is required.", nameof(name));
        }

        if (!double.IsFinite(distanceMeters) || distanceMeters < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(distanceMeters),
                distanceMeters,
                "Distance must be finite and non-negative.");
        }

        Name = name;
        DistanceMeters = distanceMeters;
    }

    public string Name { get; }

    public double DistanceMeters { get; }
}
