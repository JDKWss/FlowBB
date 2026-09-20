using FlowBB.Application.AirQuality;

namespace FlowBB.Api.Endpoints.AirQuality;

public sealed record AirQualityStationResponse(string Name, double DistanceMeters);

public sealed record AirQualityMeasurementResponse(double Value, string Unit);

public sealed record AirQualityAlertResponse(string Severity, string Message);

public sealed record AirQualityResponse(
    Guid EventId,
    AirQualityStationResponse Station,
    DateTimeOffset MeasuredAt,
    string QualityLevel,
    string Status,
    string Source,
    AirQualityMeasurementResponse? Pm10,
    AirQualityMeasurementResponse? Pm25,
    AirQualityMeasurementResponse? No2,
    AirQualityMeasurementResponse? O3,
    AirQualityAlertResponse? Alert);

public static class AirQualityResponseMapping
{
    public static AirQualityResponse ToResponse(this EventAirQuality source) => new(
        source.EventId,
        new AirQualityStationResponse(source.Station.Name, source.Station.DistanceMeters),
        source.MeasuredAt,
        source.QualityLevel.ToString(),
        source.Status.ToString(),
        source.Source.ToString(),
        ToResponse(source.Pm10),
        ToResponse(source.Pm25),
        ToResponse(source.No2),
        ToResponse(source.O3),
        source.Alert is null
            ? null
            : new AirQualityAlertResponse(source.Alert.Severity.ToString(), source.Alert.Message));

    private static AirQualityMeasurementResponse? ToResponse(AirQualityMeasurement? source) =>
        source is null ? null : new AirQualityMeasurementResponse(source.Value, source.Unit);
}
