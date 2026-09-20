using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlowBB.Application.Abstractions.AirQuality;
using FlowBB.Application.AirQuality;
using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FlowBB.Infrastructure.AirQuality;

public sealed class GiosAirQualityProvider(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<GiosAirQualityProvider> logger) : IAirQualityProvider
{
    private const string StationsCacheKey = "air-quality:gios:stations";
    private static readonly TimeSpan MetadataCacheLifetime = TimeSpan.FromHours(12);
    private static readonly TimeZoneInfo WarsawTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim StationsGate = new(1, 1);
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> SensorsGates = new();

    public async Task<AirQualityReading?> GetAsync(
        GeoPoint eventLocation,
        CancellationToken cancellationToken = default)
    {
        var selected = await FindNearestSupportedStationAsync(eventLocation, cancellationToken);
        var pm10 = await ReadMeasurementAsync(selected.Sensors, "PM10", cancellationToken);
        var pm25 = await ReadMeasurementAsync(selected.Sensors, "PM2.5", cancellationToken);
        var no2 = await ReadMeasurementAsync(selected.Sensors, "NO2", cancellationToken);
        var o3 = await ReadMeasurementAsync(selected.Sensors, "O3", cancellationToken);
        var measurements = new[] { pm10, pm25, no2, o3 };
        var available = measurements.OfType<TimedMeasurement>().ToArray();

        if (available.Length == 0)
        {
            throw new AirQualityProviderException("GIOŚ returned no usable supported measurements.");
        }

        var qualityLevel = await ReadQualityLevelAsync(selected.Station.Id, cancellationToken);
        logger.LogInformation(
            "GIOŚ Air Quality data loaded from station {StationId} with {MeasurementCount} supported measurements.",
            selected.Station.Id,
            available.Length);

        return new AirQualityReading(
            new AirQualityStation(selected.Station.Name, selected.DistanceMeters),
            available.Max(item => item.MeasuredAt),
            qualityLevel,
            pm10?.Measurement,
            pm25?.Measurement,
            no2?.Measurement,
            o3?.Measurement);
    }

    private async Task<SelectedStation> FindNearestSupportedStationAsync(
        GeoPoint eventLocation,
        CancellationToken cancellationToken)
    {
        var stations = await GetStationsAsync(cancellationToken);
        var candidates = stations
            .Select(TryMapStation)
            .OfType<Station>()
            .Select(station => new
            {
                Station = station,
                Distance = Math.Round(GeoDistance.KilometersBetween(
                    eventLocation,
                    new GeoPoint(station.Latitude, station.Longitude)) * 1000)
            })
            .OrderBy(candidate => candidate.Distance);

        foreach (var candidate in candidates)
        {
            var sensors = (await GetSensorsAsync(candidate.Station.Id, cancellationToken))
                .Where(sensor => SupportedCode(sensor.Code) is not null)
                .ToArray();
            if (sensors.Length > 0)
            {
                return new SelectedStation(candidate.Station, candidate.Distance, sensors);
            }
        }

        throw new AirQualityProviderException("GIOŚ returned no nearby station with supported measurements.");
    }

    private async Task<IReadOnlyList<StationDto>> GetStationsAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue<IReadOnlyList<StationDto>>(StationsCacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        await StationsGate.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue<IReadOnlyList<StationDto>>(StationsCacheKey, out cached) && cached is not null)
            {
                return cached;
            }

            var response = await GetAsync<StationsResponse>(
                "v1/rest/station/findAll?page=0&size=500",
                cancellationToken);
            var stations = response.Stations is { Count: > 0 }
                ? response.Stations
                : throw new AirQualityProviderException("GIOŚ returned no station metadata.");
            cache.Set(StationsCacheKey, stations, MetadataCacheLifetime);
            return stations;
        }
        finally
        {
            StationsGate.Release();
        }
    }

    private async Task<IReadOnlyList<SensorDto>> GetSensorsAsync(
        int stationId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"air-quality:gios:sensors:{stationId}";
        if (cache.TryGetValue<IReadOnlyList<SensorDto>>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var gate = SensorsGates.GetOrAdd(stationId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue<IReadOnlyList<SensorDto>>(cacheKey, out cached) && cached is not null)
            {
                return cached;
            }

            var response = await GetAsync<SensorsResponse>(
                $"v1/rest/station/sensors/{stationId}",
                cancellationToken);
            var sensors = response.Sensors ?? [];
            cache.Set(cacheKey, sensors, MetadataCacheLifetime);
            return sensors;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<TimedMeasurement?> ReadMeasurementAsync(
        IReadOnlyList<SensorDto> sensors,
        string pollutant,
        CancellationToken cancellationToken)
    {
        foreach (var sensor in sensors.Where(sensor => SupportedCode(sensor.Code) == pollutant))
        {
            try
            {
                var response = await GetAsync<MeasurementsResponse>(
                    $"v1/rest/data/getData/{sensor.Id}",
                    cancellationToken);
                var measurement = (response.Measurements ?? [])
                    .Select(TryMapMeasurement)
                    .OfType<TimedMeasurement>()
                    .OrderByDescending(item => item.MeasuredAt)
                    .FirstOrDefault();
                if (measurement is not null)
                {
                    return measurement;
                }
            }
            catch (AirQualityProviderException error)
            {
                logger.LogWarning(
                    error,
                    "GIOŚ measurement request failed for station sensor {SensorId} and pollutant {Pollutant}.",
                    sensor.Id,
                    pollutant);
            }
        }

        return null;
    }

    private async Task<AirQualityLevel> ReadQualityLevelAsync(
        int stationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await GetAsync<IndexResponse>(
                $"v1/rest/aqindex/getIndex/{stationId}",
                cancellationToken);
            return response.Index?.Category switch
            {
                "Bardzo dobry" => AirQualityLevel.VeryGood,
                "Dobry" => AirQualityLevel.Good,
                "Umiarkowany" => AirQualityLevel.Moderate,
                "Dostateczny" => AirQualityLevel.Sufficient,
                "Zły" or "Zly" => AirQualityLevel.Bad,
                "Bardzo zły" or "Bardzo zly" => AirQualityLevel.VeryBad,
                _ => AirQualityLevel.Unknown
            };
        }
        catch (AirQualityProviderException error)
        {
            logger.LogWarning(error, "GIOŚ station index request failed for station {StationId}.", stationId);
            return AirQualityLevel.Unknown;
        }
    }

    private async Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await httpClient.GetAsync(relativeUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new AirQualityProviderException(
                    $"GIOŚ request failed with HTTP status {(int)response.StatusCode}.");
            }

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                ?? throw new AirQualityProviderException("GIOŚ returned an empty response.");
        }
        catch (AirQualityProviderException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or NotSupportedException)
        {
            throw new AirQualityProviderException("GIOŚ returned an unavailable or invalid response.", error);
        }
    }

    private static Station? TryMapStation(StationDto source)
    {
        if (source.Id <= 0
            || string.IsNullOrWhiteSpace(source.Name)
            || !double.TryParse(source.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
            || !double.TryParse(source.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
            || !double.IsFinite(latitude)
            || !double.IsFinite(longitude)
            || latitude is < -90 or > 90
            || longitude is < -180 or > 180)
        {
            return null;
        }

        return new Station(source.Id, source.Name, latitude, longitude);
    }

    private static TimedMeasurement? TryMapMeasurement(MeasurementDto source)
    {
        if (source.Value is not { } value
            || !double.IsFinite(value)
            || value < 0
            || !DateTime.TryParseExact(
                source.MeasuredAt,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var localTime))
        {
            return null;
        }

        var unspecified = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
        var measuredAt = new DateTimeOffset(unspecified, WarsawTimeZone.GetUtcOffset(unspecified));
        return new TimedMeasurement(measuredAt, new AirQualityMeasurement(value));
    }

    private static string? SupportedCode(string? code) =>
        code?.Trim().Replace(',', '.') switch
        {
            "PM10" => "PM10",
            "PM2.5" => "PM2.5",
            "NO2" => "NO2",
            "O3" => "O3",
            _ => null
        };

    private sealed record Station(int Id, string Name, double Latitude, double Longitude);

    private sealed record SelectedStation(Station Station, double DistanceMeters, IReadOnlyList<SensorDto> Sensors);

    private sealed record TimedMeasurement(DateTimeOffset MeasuredAt, AirQualityMeasurement Measurement);

    private sealed record StationsResponse(
        [property: JsonPropertyName("Lista stacji pomiarowych")] IReadOnlyList<StationDto>? Stations);

    private sealed record StationDto(
        [property: JsonPropertyName("Identyfikator stacji")] int Id,
        [property: JsonPropertyName("Nazwa stacji")] string Name,
        [property: JsonPropertyName("WGS84 φ N")] string Latitude,
        [property: JsonPropertyName("WGS84 λ E")] string Longitude);

    private sealed record SensorsResponse(
        [property: JsonPropertyName("Lista stanowisk pomiarowych dla podanej stacji")]
        IReadOnlyList<SensorDto>? Sensors);

    private sealed record SensorDto(
        [property: JsonPropertyName("Identyfikator stanowiska")] int Id,
        [property: JsonPropertyName("Wskaźnik - kod")] string? Code);

    private sealed record MeasurementsResponse(
        [property: JsonPropertyName("Lista danych pomiarowych")] IReadOnlyList<MeasurementDto>? Measurements);

    private sealed record MeasurementDto(
        [property: JsonPropertyName("Data")] string? MeasuredAt,
        [property: JsonPropertyName("Wartość")] double? Value);

    private sealed record IndexResponse([property: JsonPropertyName("AqIndex")] IndexDto? Index);

    private sealed record IndexDto(
        [property: JsonPropertyName("Nazwa kategorii indeksu")] string? Category);
}
