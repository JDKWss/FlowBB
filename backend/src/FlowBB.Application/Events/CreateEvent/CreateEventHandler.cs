using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Domain.Common;
using FlowBB.Domain.Events;

namespace FlowBB.Application.Events.CreateEvent;

public sealed record CreateEventCommand(
    string Name,
    string Description,
    DateTimeOffset StartAt,
    DateTimeOffset? EndAt,
    string VenueName,
    EventCategory Category,
    double Latitude,
    double Longitude);

public sealed record CreateEventResult(EventDetails? Details, string? ValidationError)
{
    public bool IsValid => Details is not null;

    public static CreateEventResult Created(EventDetails details) => new(details, null);

    public static CreateEventResult Invalid(string error) => new(null, error);
}

public sealed class CreateEventHandler(IEventWriter writer)
{
    public async Task<CreateEventResult> HandleAsync(
        CreateEventCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Event @event;
        try
        {
            @event = new Event(
                Guid.NewGuid(),
                command.Name,
                command.Description,
                command.StartAt,
                command.EndAt,
                command.VenueName,
                command.Category,
                EventSource.External,
                new GeoPoint(command.Latitude, command.Longitude));
        }
        catch (ArgumentException exception)
        {
            return CreateEventResult.Invalid(exception.Message);
        }

        var venueId = $"external-venue-{Guid.NewGuid():N}";
        await writer.CreateAsync(@event, venueId, cancellationToken);

        return CreateEventResult.Created(
            EventDetails.From(new EventWithParticipants(@event, participantsCount: 0)));
    }
}
