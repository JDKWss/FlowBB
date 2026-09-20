using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;
using Microsoft.Extensions.Logging;

namespace FlowBB.Infrastructure.Routing;

public sealed class RoutingServiceClient(
    HttpClient httpClient,
    ILogger<RoutingServiceClient> logger)
{
    public async Task<RoadRouteCalculation> CalculateAsync(
        RouteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var mode = RoadMode(request.Mode);
        var payload = new RoutingCalculationRequest(
            new RoutingCoordinate(request.Origin.Latitude, request.Origin.Longitude),
            new RoutingCoordinate(request.Destination.Latitude, request.Destination.Longitude),
            mode);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();

        try
        {
            using var response = await httpClient.PostAsJsonAsync("route", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await ThrowServiceErrorAsync(response, cancellationToken);
            }

            var result = await response.Content.ReadFromJsonAsync<RoutingCalculationResponse>(cancellationToken);
            var validated = Validate(result, mode);
            logger.LogInformation(
                "Routing service call completed for {Mode} in {ElapsedMilliseconds} ms.",
                mode,
                System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return validated;
        }
        catch (RoutingServiceException error)
        {
            logger.LogWarning(
                "Routing service call failed for {Mode} with category {Failure}.",
                mode,
                error.Failure);
            throw;
        }
        catch (OperationCanceledException error) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RoutingServiceException(RoutingServiceFailure.Timeout, "Routing service timed out.", error);
        }
        catch (HttpRequestException error)
        {
            throw new RoutingServiceException(
                RoutingServiceFailure.TransportFailure,
                "Routing service is unavailable.",
                error);
        }
        catch (JsonException error)
        {
            throw InvalidResponse(error);
        }
        catch (NotSupportedException error)
        {
            throw InvalidResponse(error);
        }
    }

    private static string RoadMode(TransportMode mode) => mode switch
    {
        TransportMode.Walking => "Walking",
        TransportMode.Bike => "Bike",
        TransportMode.Car => "Car",
        _ => throw new RoutingServiceException(
            RoutingServiceFailure.UnsupportedMode,
            "The road-routing service supports only Walking, Bike, and Car.")
    };

    private static RoadRouteCalculation Validate(RoutingCalculationResponse? response, string expectedMode)
    {
        var geometry = response?.Geometry;
        var coordinates = geometry?.Coordinates;
        if (response is null
            || response.Mode != expectedMode
            || response.DistanceMeters <= 0
            || !double.IsFinite(response.DistanceMeters)
            || response.DurationSeconds <= 0
            || geometry?.Type != "LineString"
            || coordinates is null
            || coordinates.Count < 2
            || coordinates.Any(IsInvalidCoordinate))
        {
            throw InvalidResponse();
        }

        return new RoadRouteCalculation(
            response.Mode,
            response.DistanceMeters,
            response.DurationSeconds,
            coordinates.Select(coordinate => (IReadOnlyList<double>)coordinate!).ToArray());
    }

    private static bool IsInvalidCoordinate(IReadOnlyList<double>? coordinate) =>
        coordinate is null
        || coordinate.Count != 2
        || !double.IsFinite(coordinate[0])
        || !double.IsFinite(coordinate[1])
        || coordinate[0] is < -180 or > 180
        || coordinate[1] is < -90 or > 90;

    private static async Task ThrowServiceErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        RoutingErrorResponse? error;
        try
        {
            error = await response.Content.ReadFromJsonAsync<RoutingErrorResponse>(cancellationToken);
        }
        catch (JsonException exception)
        {
            throw InvalidResponse(exception);
        }
        catch (NotSupportedException exception)
        {
            throw InvalidResponse(exception);
        }

        var failure = MapFailure(response.StatusCode, error?.Code);
        throw new RoutingServiceException(failure, error?.Message ?? "Routing service request failed.");
    }

    private static RoutingServiceException InvalidResponse(Exception? innerException = null) => new(
        RoutingServiceFailure.InvalidResponse,
        "Routing service returned an invalid response.",
        innerException);

    private static RoutingServiceFailure MapFailure(HttpStatusCode statusCode, string? code) => code switch
    {
        "INVALID_INPUT" => RoutingServiceFailure.InvalidInput,
        "UNSUPPORTED_MODE" => RoutingServiceFailure.UnsupportedMode,
        "GRAPH_NOT_READY" => RoutingServiceFailure.GraphNotReady,
        "SNAP_TOO_FAR" => RoutingServiceFailure.SnapTooFar,
        "ROUTE_NOT_FOUND" => RoutingServiceFailure.RouteNotFound,
        "ROUTING_FAILED" => RoutingServiceFailure.RoutingFailed,
        _ when statusCode == HttpStatusCode.ServiceUnavailable => RoutingServiceFailure.GraphNotReady,
        _ => RoutingServiceFailure.InvalidResponse
    };
}
