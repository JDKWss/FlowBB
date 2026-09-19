using FlowBB.Domain.Common;

namespace FlowBB.Domain.Routing;

/// <summary>
/// Wejscie planera trasy. Zawiera wszystko, czego planer potrzebuje, wiec wynik zalezy wylacznie od tego wejscia.
/// <c>Origin</c> to wewnetrzny punkt startu uzytkownika i nie opuszcza backendu. <c>Mode</c> nie moze byc
/// <see cref="TransportMode.Unknown"/> (oznacza brakujacy lub niepoprawny tryb), wiec walidacja nalezy do wywolujacego.
/// </summary>
public sealed record RouteRequest
{
    public RouteRequest(
        Guid eventId,
        DateTimeOffset eventStartAt,
        DateTimeOffset? eventEndAt,
        GeoPoint destination,
        GeoPoint origin,
        TransportMode mode)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event id cannot be an empty GUID.", nameof(eventId));
        }

        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(origin);

        if (eventEndAt < eventStartAt)
        {
            throw new ArgumentException("EventEndAt must not be earlier than EventStartAt.", nameof(eventEndAt));
        }

        if (!Enum.IsDefined(mode) || mode == TransportMode.Unknown)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mode), mode, "Transport mode must be a known mode; Unknown means missing or invalid.");
        }

        EventId = eventId;
        EventStartAt = eventStartAt;
        EventEndAt = eventEndAt;
        Destination = destination;
        Origin = origin;
        Mode = mode;
    }

    public Guid EventId { get; }

    public DateTimeOffset EventStartAt { get; }

    public DateTimeOffset? EventEndAt { get; }

    public GeoPoint Destination { get; }

    public GeoPoint Origin { get; }

    public TransportMode Mode { get; }
}
