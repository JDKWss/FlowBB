using FlowBB.Application.Events.CreateEvent;
using FlowBB.Domain.Events;

namespace FlowBB.Api.Endpoints.Events;

public sealed record GeoPointRequest(double? Latitude, double? Longitude);

public sealed record CreateEventRequest(
    string? Name,
    string? Description,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    string? VenueName,
    string? Category,
    GeoPointRequest? Location)
{
    public bool TryToCommand(out CreateEventCommand command, out string error)
    {
        command = null!;
        error = "Request body is invalid.";

        if (Name is null || Description is null || StartAt is null || VenueName is null)
        {
            error = "Fields 'name', 'description', 'startAt', and 'venueName' are required.";
            return false;
        }

        if (Category is null || !Enum.GetNames<EventCategory>().Contains(Category, StringComparer.Ordinal))
        {
            error = "Field 'category' must be a valid EventCategory.";
            return false;
        }

        if (Location?.Latitude is null || Location.Longitude is null)
        {
            error = "Field 'location' with latitude and longitude is required.";
            return false;
        }

        command = new CreateEventCommand(
            Name,
            Description,
            StartAt.Value,
            EndAt,
            VenueName,
            Enum.Parse<EventCategory>(Category),
            Location.Latitude.Value,
            Location.Longitude.Value);
        return true;
    }
}
