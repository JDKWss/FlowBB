using System.Text.Json.Serialization;
using FlowBB.Application.Attendance.UpsertAttendance;
using FlowBB.Domain.Common;

namespace FlowBB.Api.Endpoints.Events;

// DTO odpowiadaja AttendanceUpsertRequest i AttendanceResponse z contracts/openapi.yaml.
// Tryb transportu jest serializowany jako tekst (np. "PublicTransport"), zgodnie z enumem kontraktu.
public sealed record AttendanceUpsertRequest(
    Guid UserId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TransportMode TransportMode);

public sealed record AttendanceResponse(
    Guid EventId,
    Guid UserId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TransportMode TransportMode,
    int ParticipantsCount,
    bool IsNew,
    DateTimeOffset UpdatedAt)
{
    public static AttendanceResponse From(UpsertAttendanceResult result) =>
        new(result.EventId, result.UserId, result.TransportMode, result.ParticipantsCount, result.IsNew, result.UpdatedAt);
}
