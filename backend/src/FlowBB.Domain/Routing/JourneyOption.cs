namespace FlowBB.Domain.Routing;

public sealed record JourneyOption
{
    public JourneyOption(
        int durationMinutes,
        DateTimeOffset departureAt,
        DateTimeOffset arrivalAt,
        IReadOnlyList<RouteStep> steps,
        double? distanceMeters = null,
        RouteGeometry? geometry = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(durationMinutes);
        ArgumentNullException.ThrowIfNull(steps);

        if (arrivalAt < departureAt)
        {
            throw new ArgumentException("ArrivalAt must not be earlier than DepartureAt.", nameof(arrivalAt));
        }

        if (steps.Count == 0)
        {
            throw new ArgumentException("A journey needs at least one step.", nameof(steps));
        }

        if (distanceMeters is < 0 || (distanceMeters.HasValue && !double.IsFinite(distanceMeters.Value)))
        {
            throw new ArgumentOutOfRangeException(nameof(distanceMeters), distanceMeters, "Distance must be finite and non-negative.");
        }

        DurationMinutes = durationMinutes;
        DepartureAt = departureAt;
        ArrivalAt = arrivalAt;
        Steps = steps;
        DistanceMeters = distanceMeters;
        Geometry = geometry;
    }

    public int DurationMinutes { get; }

    public DateTimeOffset DepartureAt { get; }

    public DateTimeOffset ArrivalAt { get; }

    public IReadOnlyList<RouteStep> Steps { get; }

    public double? DistanceMeters { get; }

    public RouteGeometry? Geometry { get; }
}
