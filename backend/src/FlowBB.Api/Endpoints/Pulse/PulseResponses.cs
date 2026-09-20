using FlowBB.Application.Pulse;
using FlowBB.Application.Pulse.GetActivityMap;
using FlowBB.Application.Pulse.GetEventPulse;
using FlowBB.Application.Pulse.GetPulseSummary;

namespace FlowBB.Api.Endpoints.Pulse;

// DTO odpowiadaja schematom z contracts/openapi.yaml. Zawieraja wylacznie agregaty.
public sealed record ModalSplitResponse(int PublicTransport, int Walking, int Bike, int Car, int Unknown);

public sealed record PulseAlertResponse(string Code, string Severity, string Message);

public sealed record PulseSummaryResponse(
    DateTimeOffset GeneratedAt,
    int EventsCount,
    int ParticipantsCount,
    ModalSplitResponse ModalSplit,
    int ParticipantsWithoutReturn);

public sealed record EventPulseResponse(
    Guid EventId,
    string EventName,
    DateTimeOffset GeneratedAt,
    int ParticipantsCount,
    ModalSplitResponse ModalSplit,
    int ParticipantsWithoutReturn,
    IReadOnlyList<PulseAlertResponse> Alerts);

public sealed record HexagonPropertiesResponse(int Participants, int PublicTransport, int Walking, int Bike, int Car);

public sealed record PolygonGeometryResponse(string Type, IReadOnlyList<IReadOnlyList<double[]>> Coordinates);

public sealed record HexagonFeatureResponse(
    string Type,
    string Id,
    PolygonGeometryResponse Geometry,
    HexagonPropertiesResponse Properties);

public sealed record HexagonFeatureCollectionResponse(string Type, IReadOnlyList<HexagonFeatureResponse> Features);

public static class PulseResponseMapping
{
    public static ModalSplitResponse ToResponse(this ModalSplit split) =>
        new(split.PublicTransport, split.Walking, split.Bike, split.Car, split.Unknown);

    public static PulseSummaryResponse ToResponse(this PulseSummary summary) =>
        new(summary.GeneratedAt, summary.EventsCount, summary.ParticipantsCount,
            summary.ModalSplit.ToResponse(), summary.ParticipantsWithoutReturn);

    public static EventPulseResponse ToResponse(this EventPulse pulse) =>
        new(pulse.EventId, pulse.EventName, pulse.GeneratedAt, pulse.ParticipantsCount,
            pulse.ModalSplit.ToResponse(), pulse.ParticipantsWithoutReturn, pulse.Alerts.Select(ToResponse).ToList());

    private static PulseAlertResponse ToResponse(PulseAlert alert) =>
        new(alert.Code.ToString(), alert.Severity.ToString(), alert.Message);

    public static HexagonFeatureCollectionResponse ToFeatureCollection(this IEnumerable<HexCell> cells) =>
        new("FeatureCollection", cells.Select(ToFeature).ToList());

    private static HexagonFeatureResponse ToFeature(HexCell cell) =>
        new("Feature", cell.Id, ToPolygon(cell), ToProperties(cell));

    private static HexagonPropertiesResponse ToProperties(HexCell cell) =>
        new(cell.Participants, cell.ModalSplit.PublicTransport, cell.ModalSplit.Walking,
            cell.ModalSplit.Bike, cell.ModalSplit.Car);

    // GeoJSON (RFC 7946): kolejnosc [longitude, latitude], pierscien zamkniety (pierwszy punkt = ostatni).
    private static PolygonGeometryResponse ToPolygon(HexCell cell)
    {
        var ring = cell.Vertices.Select(vertex => new[] { vertex.Longitude, vertex.Latitude }).ToList();
        ring.Add(ring[0]);
        return new PolygonGeometryResponse("Polygon", [ring]);
    }
}
