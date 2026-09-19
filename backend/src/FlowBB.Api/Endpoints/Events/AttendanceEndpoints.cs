using FlowBB.Application.Abstractions.Realtime;
using FlowBB.Application.Attendance.DeleteAttendance;
using FlowBB.Application.Attendance.UpsertAttendance;
using FlowBB.Application.Pulse.GetEventPulse;
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
        Guid eventId,
        AttendanceUpsertRequest request,
        UpsertAttendanceHandler handler,
        IPulseNotifier notifier,
        CancellationToken cancellationToken)
    {
        if (eventId == Guid.Empty || request.UserId == Guid.Empty || !Enum.IsDefined(request.TransportMode))
        {
            return BadRequest("Invalid attendance request.");
        }

        var result = await handler.HandleAsync(
            new UpsertAttendanceCommand(eventId, request.UserId, request.TransportMode), cancellationToken);
        if (result is null)
        {
            return TypedResults.Problem(title: "Event or user not found.", statusCode: StatusCodes.Status404NotFound);
        }

        // Publikacja dopiero po zatwierdzeniu zapisu. Komunikat zawiera wylacznie agregaty.
        await notifier.PublishAsync(
            new PulseUpdate(
                result.EventId,
                result.ParticipantsCount,
                result.ModalSplit,
                GetEventPulseHandler.ParticipantsWithoutReturnInMvp,
                result.UpdatedAt),
            cancellationToken);

        return TypedResults.Ok(AttendanceResponse.From(result));
    }

    private static async Task<IResult> DeleteAsync(
        Guid eventId,
        Guid userId,
        DeleteAttendanceHandler handler,
        IPulseNotifier notifier,
        CancellationToken cancellationToken)
    {
        if (eventId == Guid.Empty || userId == Guid.Empty)
        {
            return BadRequest("Invalid attendance request.");
        }

        var result = await handler.HandleAsync(new DeleteAttendanceCommand(eventId, userId), cancellationToken);

        // Brak deklaracji to no-op: nic sie nie zmienilo, wiec nie publikujemy.
        if (result.WasDeleted)
        {
            await notifier.PublishAsync(
                new PulseUpdate(
                    result.EventId,
                    result.ParticipantsCount,
                    result.ModalSplit,
                    GetEventPulseHandler.ParticipantsWithoutReturnInMvp,
                    result.ChangedAt),
                cancellationToken);
        }

        return TypedResults.NoContent();
    }

    private static IResult BadRequest(string title) =>
        TypedResults.Problem(title: title, statusCode: StatusCodes.Status400BadRequest);
}
