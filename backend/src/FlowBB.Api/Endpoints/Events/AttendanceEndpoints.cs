using FlowBB.Api.Endpoints;
using FlowBB.Application.Abstractions.Realtime;
using FlowBB.Application.Attendance.DeleteAttendance;
using FlowBB.Application.Attendance.UpsertAttendance;
using FlowBB.Domain.Common;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlowBB.Api.Endpoints.Events;

public static class AttendanceEndpoints
{
    public static IServiceCollection AddAttendanceModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<UpsertAttendanceHandler>();
        services.AddScoped<DeleteAttendanceHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/events/{eventId}/attendance").WithTags("Attendance");

        group.MapPost("/", UpsertAsync).WithName("upsertAttendance");
        group.MapDelete("/{userId}", DeleteAsync).WithName("deleteAttendance");

        return app;
    }

    private static async Task<IResult> UpsertAsync(
        string eventId,
        HttpRequest httpRequest,
        UpsertAttendanceHandler handler,
        IPulseNotifier notifier,
        CancellationToken cancellationToken)
    {
        var request = await ApiRequests.ReadJsonAsync<AttendanceUpsertRequest>(httpRequest, cancellationToken);
        if (!RouteIds.TryParse(eventId, out var eventGuid) ||
            request is null ||
            request.UserId == Guid.Empty ||
            !Enum.IsDefined(request.TransportMode))
        {
            return ApiProblems.BadRequest("Invalid attendance request.");
        }

        var result = await handler.HandleAsync(
            new UpsertAttendanceCommand(eventGuid, request.UserId, request.TransportMode), cancellationToken);
        if (result is null)
        {
            return ApiProblems.NotFound("Event or user not found.");
        }

        // Publikacja dopiero po zatwierdzeniu zapisu. Komunikat zawiera wylacznie agregaty.
        await notifier.PublishAsync(
            new PulseUpdate(
                result.EventId,
                result.ParticipantsCount,
                result.ModalSplit,
                result.ParticipantsWithoutReturn,
                result.UpdatedAt),
            cancellationToken);

        return TypedResults.Ok(AttendanceResponse.From(result));
    }

    private static async Task<IResult> DeleteAsync(
        string eventId,
        string userId,
        DeleteAttendanceHandler handler,
        IPulseNotifier notifier,
        CancellationToken cancellationToken)
    {
        if (!RouteIds.TryParse(eventId, out var eventGuid) || !RouteIds.TryParse(userId, out var userGuid))
        {
            return ApiProblems.BadRequest("Invalid attendance request.");
        }

        var result = await handler.HandleAsync(new DeleteAttendanceCommand(eventGuid, userGuid), cancellationToken);

        // Brak deklaracji to no-op: nic sie nie zmienilo, wiec nie publikujemy.
        if (result.WasDeleted)
        {
            await notifier.PublishAsync(
                new PulseUpdate(
                    result.EventId,
                    result.ParticipantsCount,
                    result.ModalSplit,
                    result.ParticipantsWithoutReturn,
                    result.ChangedAt),
                cancellationToken);
        }

        return TypedResults.NoContent();
    }
}
