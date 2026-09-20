using FlowBB.Domain.Routing;

namespace FlowBB.Api.Endpoints.Routing;

// DTO odpowiadaja schematom RouteStep, JourneyOption i RouteResponse z contracts/openapi.yaml. Enumy sa zwracane jako
// nazwy (string), a czas w strefie Europe/Warsaw. Odpowiedz nie zawiera wspolrzednych uzytkownika ani punktu startu.
public sealed record RouteStepResponse(string Type, string Instruction, int DurationMinutes, string? Line);

public sealed record JourneyOptionResponse(
    int DurationMinutes,
    DateTimeOffset DepartureAt,
    DateTimeOffset ArrivalAt,
    IReadOnlyList<RouteStepResponse> Steps);

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
            InWarsaw(option.DepartureAt),
            InWarsaw(option.ArrivalAt),
            option.Steps.Select(step => new RouteStepResponse(
                step.Type.ToString(), step.Instruction, step.DurationMinutes, step.Line)).ToList());

    private static DateTimeOffset InWarsaw(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, DemoTimeZone);
}
