using FlowBB.Domain.Common;

namespace FlowBB.Domain.Events;

public sealed record Event
{
    public const int NameMaxLength = 160;
    public const int DescriptionMaxLength = 1000;
    public const int VenueNameMaxLength = 160;

    public Event(
        Guid id,
        string name,
        string description,
        DateTimeOffset startAt,
        DateTimeOffset? endAt,
        string venueName,
        EventCategory category,
        EventSource source,
        GeoPoint location)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Event id must not be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(venueName);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(location);
        EnsureMaxLength(name, NameMaxLength, nameof(name));
        EnsureMaxLength(description, DescriptionMaxLength, nameof(description));
        EnsureMaxLength(venueName, VenueNameMaxLength, nameof(venueName));

        if (endAt < startAt)
        {
            throw new ArgumentException("EndAt must not be earlier than StartAt.", nameof(endAt));
        }

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown event category.");
        }

        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown event source.");
        }

        Id = id;
        Name = name;
        Description = description;
        StartAt = startAt;
        EndAt = endAt;
        VenueName = venueName;
        Category = category;
        Source = source;
        Location = location;
    }

    public Guid Id { get; }

    public string Name { get; }

    public string Description { get; }

    public DateTimeOffset StartAt { get; }

    public DateTimeOffset? EndAt { get; }

    public string VenueName { get; }

    public EventCategory Category { get; }

    public EventSource Source { get; }

    public GeoPoint Location { get; }

    private static void EnsureMaxLength(string value, int maxLength, string paramName)
    {
        if (value.Length > maxLength)
        {
            throw new ArgumentException($"Value must not exceed {maxLength} characters.", paramName);
        }
    }
}
