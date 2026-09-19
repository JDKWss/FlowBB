using FlowBB.Domain.Common;

namespace FlowBB.Application.Pulse;

/// <summary>
/// Wewnetrzny punkt startu deklaracji "Ide". Nie zawiera identyfikatora uzytkownika
/// i nie moze opuszczac backendu: publicznie zwracane sa wylacznie zagregowane komorki.
/// </summary>
public sealed record PulsePoint
{
    public double Latitude { get; }
    public double Longitude { get; }
    public TransportMode TransportMode { get; }

    public PulsePoint(double latitude, double longitude, TransportMode transportMode)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude))
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Coordinates must be finite numbers.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(latitude, -90, nameof(latitude));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(latitude, 90, nameof(latitude));
        ArgumentOutOfRangeException.ThrowIfLessThan(longitude, -180, nameof(longitude));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(longitude, 180, nameof(longitude));

        Latitude = latitude;
        Longitude = longitude;
        TransportMode = transportMode;
    }
}
