using FlowBB.Api.Endpoints;
using FlowBB.Application.Crews;
using FlowBB.Application.Crews.GetEventGroups;
using FlowBB.Application.Crews.JoinCrew;
using FlowBB.Application.Crews.LeaveCrew;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlowBB.Api.Endpoints.Crews;

public static class CrewsEndpoints
{
    public static IServiceCollection AddCrewModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<GetEventGroupsHandler>();
        services.AddScoped<JoinCrewHandler>();
        services.AddScoped<LeaveCrewHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapCrewEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events/{eventId}/groups", GetEventGroupsAsync)
            .WithTags("Groups").WithName("getEventGroups");
        app.MapPost("/api/groups/{groupId}/members", JoinGroupAsync)
            .WithTags("Groups").WithName("joinGroup");
        app.MapDelete("/api/groups/{groupId}/members/{userId}", LeaveGroupAsync)
            .WithTags("Groups").WithName("leaveGroup");

        return app;
    }

    private static async Task<IResult> GetEventGroupsAsync(
        string eventId, string? userId, GetEventGroupsHandler handler, CancellationToken cancellationToken)
    {
        if (!RouteIds.TryParse(eventId, out var eventGuid))
        {
            return ApiProblems.BadRequest("Path parameter 'eventId' must be a non-empty UUID.");
        }

        Guid? userGuid = null;
        if (userId is not null)
        {
            if (!RouteIds.TryParse(userId, out var parsedUser))
            {
                return ApiProblems.BadRequest("Query parameter 'userId' must be a non-empty UUID.");
            }

            userGuid = parsedUser;
        }

        var groups = await handler.HandleAsync(eventGuid, userGuid, cancellationToken);
        return groups is null
            ? ApiProblems.NotFound("Event not found.")
            : TypedResults.Ok(groups.Select(group => group.ToResponse()).ToList());
    }

    private static async Task<IResult> JoinGroupAsync(
        string groupId, HttpRequest httpRequest, JoinCrewHandler handler, CancellationToken cancellationToken)
    {
        var request = await ApiRequests.ReadJsonAsync<GroupMembershipRequest>(httpRequest, cancellationToken);
        if (!RouteIds.TryParse(groupId, out var groupGuid) || !RouteIds.TryParse(request?.UserId, out var userGuid))
        {
            return ApiProblems.BadRequest("Path parameter 'groupId' and body field 'userId' must be non-empty UUIDs.");
        }

        var result = await handler.HandleAsync(groupGuid, userGuid, cancellationToken);
        return result.Outcome switch
        {
            JoinCrewOutcome.Joined or JoinCrewOutcome.AlreadyMember => TypedResults.Ok(result.Crew!.ToResponse()),
            JoinCrewOutcome.Full => ApiProblems.Conflict("The group is full."),
            JoinCrewOutcome.InAnotherCrew => ApiProblems.Conflict("The user already belongs to another group of this event."),
            JoinCrewOutcome.CrewNotFound => ApiProblems.NotFound("Group not found."),
            _ => ApiProblems.NotFound("User not found.")
        };
    }

    private static async Task<IResult> LeaveGroupAsync(
        string groupId, string userId, LeaveCrewHandler handler, CancellationToken cancellationToken)
    {
        if (!RouteIds.TryParse(groupId, out var groupGuid) || !RouteIds.TryParse(userId, out var userGuid))
        {
            return ApiProblems.BadRequest("Path parameters 'groupId' and 'userId' must be non-empty UUIDs.");
        }

        await handler.HandleAsync(groupGuid, userGuid, cancellationToken);
        return TypedResults.NoContent();
    }
}
