namespace FlowBB.Infrastructure.Routing;

internal sealed record RoutingCoordinate(double Latitude, double Longitude);

internal sealed record RoutingCalculationRequest(
    RoutingCoordinate Origin,
    RoutingCoordinate Destination,
    string Mode);

internal sealed record RoutingLineString(
    string? Type,
    IReadOnlyList<IReadOnlyList<double>?>? Coordinates);

internal sealed record RoutingCalculationResponse(
    string? Mode,
    double DistanceMeters,
    int DurationSeconds,
    RoutingLineString? Geometry);

internal sealed record RoutingErrorResponse(string Code, string Message);

public sealed record RoadRouteCalculation(
    string Mode,
    double DistanceMeters,
    int DurationSeconds,
    IReadOnlyList<IReadOnlyList<double>> Coordinates);

public enum RoutingServiceFailure
{
    InvalidInput,
    UnsupportedMode,
    GraphNotReady,
    SnapTooFar,
    RouteNotFound,
    RoutingFailed,
    TransportFailure,
    Timeout,
    InvalidResponse
}

public sealed class RoutingServiceException(
    RoutingServiceFailure failure,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public RoutingServiceFailure Failure { get; } = failure;
}
