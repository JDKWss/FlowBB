namespace FlowBB.Application.AirQuality;

/// <summary>
/// Normalized output of an external air-quality provider. Public response status,
/// source and fallback selection belong to the application use case.
/// </summary>
public sealed record AirQualityReading
{
    public AirQualityReading(
        AirQualityStation station,
        DateTimeOffset measuredAt,
        AirQualityLevel qualityLevel,
        AirQualityMeasurement? pm10,
        AirQualityMeasurement? pm25,
        AirQualityMeasurement? no2,
        AirQualityMeasurement? o3)
    {
        if (pm10 is null && pm25 is null && no2 is null && o3 is null)
        {
            throw new ArgumentException("At least one supported pollutant measurement is required.");
        }

        Station = station ?? throw new ArgumentNullException(nameof(station));
        MeasuredAt = measuredAt;
        QualityLevel = qualityLevel;
        Pm10 = pm10;
        Pm25 = pm25;
        No2 = no2;
        O3 = o3;
    }

    public AirQualityStation Station { get; }

    public DateTimeOffset MeasuredAt { get; }

    public AirQualityLevel QualityLevel { get; }

    public AirQualityMeasurement? Pm10 { get; }

    public AirQualityMeasurement? Pm25 { get; }

    public AirQualityMeasurement? No2 { get; }

    public AirQualityMeasurement? O3 { get; }
}
