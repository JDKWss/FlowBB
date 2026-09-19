using FlowBB.Domain.Common;
using FlowBB.Domain.Events;

namespace FlowBB.Application.Events;

/// <summary>
/// Szczegoly wydarzenia zgodne z <c>EventDetails</c> z OpenAPI. Crew i lista srodkow transportu nie maja jeszcze
/// zrodla w Neo4j, wiec w MVP sa to wartosci stale (do zmiany w jednym miejscu po wdrozeniu Crew).
/// </summary>
public sealed record EventDetails(
    Event Event,
    int ParticipantsCount,
    bool CrewAvailable,
    IReadOnlyList<TransportMode> AvailableTransportModes)
{
    public static IReadOnlyList<TransportMode> DefaultTransportModes { get; } =
        [TransportMode.Walking, TransportMode.PublicTransport, TransportMode.Bike, TransportMode.Car];

    public static EventDetails From(EventWithParticipants source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new EventDetails(source.Event, source.ParticipantsCount, false, DefaultTransportModes);
    }
}
