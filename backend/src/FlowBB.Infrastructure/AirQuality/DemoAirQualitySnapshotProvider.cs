using System.Reflection;
using System.Text.Json;
using FlowBB.Application.Abstractions.AirQuality;
using FlowBB.Application.AirQuality;
using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;

namespace FlowBB.Infrastructure.AirQuality;

public sealed class DemoAirQualitySnapshotProvider : IAirQualityFallbackProvider
{
    private const string ResourceName = "FlowBB.Data.AirQuality.demo-snapshot.json";
    private const double StationLatitude = 49.813464;
    private const double StationLongitude = 19.027318;
    private static readonly GeoPoint StationLocation = new(StationLatitude, StationLongitude);

    private readonly Snapshot _snapshot;

    public DemoAirQualitySnapshotProvider()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded Air Quality snapshot '{ResourceName}' was not found.");
        _snapshot = JsonSerializer.Deserialize<Snapshot>(stream, JsonOptions())
            ?? throw new InvalidDataException("Air Quality demo snapshot is empty.");
        Validate(_snapshot);
    }

    public Task<EventAirQuality> GetAsync(
        Guid eventId,
        GeoPoint eventLocation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var station = new AirQualityStation(
            _snapshot.Station.Name,
            Math.Round(GeoDistance.KilometersBetween(eventLocation, StationLocation) * 1000));

        var result = new EventAirQuality(
            eventId,
            station,
            _snapshot.MeasuredAt,
            ParseEnum<AirQualityLevel>(_snapshot.QualityLevel, "qualityLevel"),
            AirQualityStatus.Fallback,
            AirQualitySource.Demo,
            ToMeasurement(_snapshot.Pm10),
            ToMeasurement(_snapshot.Pm25),
            ToMeasurement(_snapshot.No2),
            ToMeasurement(_snapshot.O3),
            ToAlert(_snapshot.Alert));
        return Task.FromResult(result);
    }

    private static AirQualityMeasurement? ToMeasurement(Measurement? source) =>
        source is null ? null : new AirQualityMeasurement(source.Value);

    private static AirQualityAlert? ToAlert(Alert? source) =>
        source is null
            ? null
            : new AirQualityAlert(
                ParseEnum<AirQualityAlertSeverity>(source.Severity, "alert.severity"),
                source.Message);

    private static void Validate(Snapshot snapshot)
    {
        if (snapshot.EventId == Guid.Empty
            || string.IsNullOrWhiteSpace(snapshot.Station.Name)
            || snapshot.Status != nameof(AirQualityStatus.Fallback)
            || snapshot.Source != nameof(AirQualitySource.Demo)
            || snapshot.Pm10 is null && snapshot.Pm25 is null && snapshot.No2 is null && snapshot.O3 is null)
        {
            throw new InvalidDataException("Air Quality demo snapshot has invalid fallback semantics.");
        }

        _ = ParseEnum<AirQualityLevel>(snapshot.QualityLevel, "qualityLevel");
        foreach (var measurement in new[] { snapshot.Pm10, snapshot.Pm25, snapshot.No2, snapshot.O3 }.OfType<Measurement>())
        {
            if (!double.IsFinite(measurement.Value)
                || measurement.Value < 0
                || measurement.Unit != AirQualityMeasurement.CanonicalUnit)
            {
                throw new InvalidDataException("Air Quality demo snapshot contains an invalid measurement.");
            }
        }
    }

    private static T ParseEnum<T>(string value, string propertyName)
        where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new InvalidDataException($"Air Quality demo snapshot contains invalid {propertyName}.");

    private static JsonSerializerOptions JsonOptions() => new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false
    };

    private sealed record Snapshot(
        Guid EventId,
        Station Station,
        DateTimeOffset MeasuredAt,
        string QualityLevel,
        string Status,
        string Source,
        Measurement? Pm10,
        Measurement? Pm25,
        Measurement? No2,
        Measurement? O3,
        Alert? Alert);

    private sealed record Station(string Name, double DistanceMeters);

    private sealed record Measurement(double Value, string Unit);

    private sealed record Alert(string Severity, string Message);
}
