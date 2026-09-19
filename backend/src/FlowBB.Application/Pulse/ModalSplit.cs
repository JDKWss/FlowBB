using FlowBB.Domain.Common;

namespace FlowBB.Application.Pulse;

public sealed record ModalSplit(int PublicTransport, int Walking, int Bike, int Car, int Unknown)
{
    public static ModalSplit From(IEnumerable<PulsePoint> points)
    {
        int publicTransport = 0, walking = 0, bike = 0, car = 0, unknown = 0;

        foreach (var point in points)
        {
            switch (point.TransportMode)
            {
                case TransportMode.PublicTransport: publicTransport++; break;
                case TransportMode.Walking: walking++; break;
                case TransportMode.Bike: bike++; break;
                case TransportMode.Car: car++; break;
                default: unknown++; break;
            }
        }

        return new ModalSplit(publicTransport, walking, bike, car, unknown);
    }
}
