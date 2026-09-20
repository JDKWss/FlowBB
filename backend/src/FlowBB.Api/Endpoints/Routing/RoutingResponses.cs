using FlowBB.Domain.Routing;

namespace FlowBB.Api.Endpoints.Routing;

// DTO odpowiadaja schematom RouteStep, JourneyOption i RouteResponse z contracts/openapi.yaml. Enumy sa zwracane jako
// nazwy (string), a czas w strefie Europe/Warsaw. Profil uzytkownika ani osobne pole origin nie sa ujawniane;
// mieszkaniec otrzymuje jedynie geometrie swojej wyliczonej trasy.
public sealed record RouteStepResponse(string Type, string Instruction, int DurationMinutes, string? Line);

public sealed record RouteGeometryResponse(string Type, IReadOnlyList<IReadOnlyList<double>> Coordinates);

// Wspolrzedne publicznej infrastruktury przystankowej. Punkt startu uzytkownika nie jest przystankiem i tu nie trafia.
public sealed record RouteStopResponse(string Name, double Latitude, double Longitude);

public sealed record JourneyOptionResponse(
    int DurationMinutes,
    double? DistanceMeters,
    DateTimeOffset DepartureAt,
    DateTimeOffset ArrivalAt,
    RouteGeometryResponse? Geometry,
    IReadOnlyList<RouteStepResponse> Steps,
    IReadOnlyList<RouteStopResponse>? Stops);

public sealed record RouteResponse(
    Guid EventId,
    Guid UserId,
    string PlannerSource,
    JourneyOptionResponse Outbound,
    IReadOnlyList<JourneyOptionResponse> Returns,
    bool ReturnGap);

public static class RouteResponseMapping
{
    private static readonly TimeZoneInfo DemoTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    public static RouteResponse ToResponse(this RoutePlan plan, Guid eventId, Guid userId) =>
        new(
            eventId,
            userId,
            plan.Source.ToString(),
            plan.Outbound.ToResponse(),
            plan.Returns.Select(option => option.ToResponse()).ToList(),
            plan.ReturnGap);

    private static JourneyOptionResponse ToResponse(this JourneyOption option) =>
        new(
            option.DurationMinutes,
            option.DistanceMeters,
            InWarsaw(option.DepartureAt),
            InWarsaw(option.ArrivalAt),
            option.Geometry is null
                ? null
                : new RouteGeometryResponse(
                    option.Geometry.Type,
                    option.Geometry.Coordinates
                        .Select(coordinate => (IReadOnlyList<double>)[coordinate.Longitude, coordinate.Latitude])
                        .ToList()),
            option.Steps.Select(step => new RouteStepResponse(
                step.Type.ToString(), step.Instruction, step.DurationMinutes, step.Line)).ToList(),
            option.Stops?.Select(stop => new RouteStopResponse(
                stop.Name, stop.Location.Latitude, stop.Location.Longitude)).ToList());

    private static DateTimeOffset InWarsaw(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, DemoTimeZone);
}
