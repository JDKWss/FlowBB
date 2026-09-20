namespace FlowBB.Application.AirQuality;

public enum AirQualityStatus
{
    Fresh,
    Stale,
    Fallback
}

public enum AirQualitySource
{
    Gios,
    Demo
}

public enum AirQualityAlertSeverity
{
    Info,
    Warning
}

public sealed record AirQualityAlert
{
    public AirQualityAlert(AirQualityAlertSeverity severity, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Severity = severity;
        Message = message;
    }

    public AirQualityAlertSeverity Severity { get; }

    public string Message { get; }
}

public sealed record EventAirQuality(
    Guid EventId,
    AirQualityStation Station,
    DateTimeOffset MeasuredAt,
    AirQualityLevel QualityLevel,
    AirQualityStatus Status,
    AirQualitySource Source,
    AirQualityMeasurement? Pm10,
    AirQualityMeasurement? Pm25,
    AirQualityMeasurement? No2,
    AirQualityMeasurement? O3,
    AirQualityAlert? Alert);
