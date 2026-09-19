using FlowBB.Application.Events;
using FlowBB.Domain.Common;

namespace FlowBB.Api.Endpoints.Events;

// DTO odpowiadaja schematom EventSummary i EventDetails z contracts/openapi.yaml. Enumy sa zwracane jako nazwy (string),
// a czas w strefie Europe/Warsaw. Odpowiedz nie zawiera identyfikatorow ani wspolrzednych uzytkownikow.
public sealed record GeoPointResponse(double Latitude, double Longitude);

public record EventSummaryResponse(
    Guid Id,
    string Name,
    string Description,
    DateTimeOffset StartAt,
    DateTimeOffset? EndAt,
    string VenueName,
    string Category,
    GeoPointResponse Location,
    int ParticipantsCount,
    string Source);

public sealed record EventDetailsResponse(
    Guid Id,
    string Name,
    string Description,
    DateTimeOffset StartAt,
    DateTimeOffset? EndAt,
    string VenueName,
    string Category,
    GeoPointResponse Location,
    int ParticipantsCount,
    string Source,
    bool CrewAvailable,
    IReadOnlyList<string> AvailableTransportModes)
    : EventSummaryResponse(
        Id, Name, Description, StartAt, EndAt, VenueName, Category, Location, ParticipantsCount, Source);

public static class EventResponseMapping
{
    private static readonly TimeZoneInfo DemoTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    public static EventSummaryResponse ToSummaryResponse(this EventWithParticipants source)
    {
        var item = source.Event;
        return new EventSummaryResponse(
            item.Id,
            item.Name,
            item.Description,
            InWarsaw(item.StartAt),
            InWarsaw(item.EndAt),
            item.VenueName,
            item.Category.ToString(),
            ToLocation(item.Location),
            source.ParticipantsCount,
            item.Source.ToString());
    }

    public static EventDetailsResponse ToResponse(this EventDetails details)
    {
        var item = details.Event;
        return new EventDetailsResponse(
            item.Id,
            item.Name,
            item.Description,
            InWarsaw(item.StartAt),
            InWarsaw(item.EndAt),
            item.VenueName,
            item.Category.ToString(),
            ToLocation(item.Location),
            details.ParticipantsCount,
            item.Source.ToString(),
            details.CrewAvailable,
            details.AvailableTransportModes.Select(mode => mode.ToString()).ToList());
    }

    private static GeoPointResponse ToLocation(GeoPoint point) => new(point.Latitude, point.Longitude);

    private static DateTimeOffset InWarsaw(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, DemoTimeZone);

    private static DateTimeOffset? InWarsaw(DateTimeOffset? value) => value is null ? null : InWarsaw(value.Value);
}
