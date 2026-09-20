namespace FlowBB.Domain.Routing;

public sealed record JourneyOption
{
    public JourneyOption(
        int durationMinutes,
        DateTimeOffset departureAt,
        DateTimeOffset arrivalAt,
        IReadOnlyList<RouteStep> steps,
        double? distanceMeters = null,
        RouteGeometry? geometry = null,
        IReadOnlyList<RouteStop>? stops = null)
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

        if (stops is { Count: 1 })
        {
            throw new ArgumentException("A transit leg needs a boarding and an alighting stop.", nameof(stops));
        }

        DurationMinutes = durationMinutes;
        DepartureAt = departureAt;
        ArrivalAt = arrivalAt;
        Steps = steps;
        DistanceMeters = distanceMeters;
        Geometry = geometry;
        Stops = stops is { Count: > 0 } ? [.. stops] : null;
    }

    public int DurationMinutes { get; }

    public DateTimeOffset DepartureAt { get; }

    public DateTimeOffset ArrivalAt { get; }

    public IReadOnlyList<RouteStep> Steps { get; }

    public double? DistanceMeters { get; }

    public RouteGeometry? Geometry { get; }

    /// <summary>
    /// Przystanki odcinka komunikacji miejskiej w kolejnosci przejazdu: pierwszy to przystanek wsiadania, ostatni
    /// wysiadania. <c>null</c> dla trybow drogowych i dla planera demonstracyjnego.
    /// </summary>
    public IReadOnlyList<RouteStop>? Stops { get; }
}
