namespace FlowBB.Domain.Crews;

public sealed record MeetingPoint
{
    public const int MaxNameLength = 120;

    public string Name { get; }
    public double Latitude { get; }
    public double Longitude { get; }

    public MeetingPoint(string name, double latitude, double longitude)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Trim().Length, MaxNameLength, nameof(name));
        ArgumentOutOfRangeException.ThrowIfLessThan(latitude, -90, nameof(latitude));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(latitude, 90, nameof(latitude));
        ArgumentOutOfRangeException.ThrowIfLessThan(longitude, -180, nameof(longitude));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(longitude, 180, nameof(longitude));

        Name = name.Trim();
        Latitude = latitude;
        Longitude = longitude;
    }
}
