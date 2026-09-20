namespace FlowBB.Application.AirQuality;

public sealed record AirQualityMeasurement
{
    public const string CanonicalUnit = "µg/m³";

    public AirQualityMeasurement(double value)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Concentration must be finite and non-negative.");
        }

        Value = value;
    }

    public double Value { get; }

    public string Unit => CanonicalUnit;
}
