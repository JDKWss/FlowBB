namespace FlowBB.Domain.Crews;

public sealed class Crew
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 500;
    public const int MinMembersLimit = 2;
    public const int MaxMembersLimit = 12;
    public const int MaxTags = 10;
    public const int MaxTagLength = 40;

    private readonly List<Guid> _members = [];
    private readonly List<string> _tags;

    public Guid Id { get; }
    public Guid EventId { get; }
    public string Name { get; }
    public string Description { get; }
    public int MaxMembers { get; }
    public MeetingPoint MeetingPoint { get; }
    public IReadOnlyList<string> Tags => _tags.AsReadOnly();
    public IReadOnlyList<Guid> Members => _members.AsReadOnly();
    public int CurrentMembers => _members.Count;

    private Crew(
        Guid id,
        Guid eventId,
        string name,
        string description,
        int maxMembers,
        List<string> tags,
        MeetingPoint meetingPoint)
    {
        Id = id;
        EventId = eventId;
        Name = name;
        Description = description;
        MaxMembers = maxMembers;
        _tags = tags;
        MeetingPoint = meetingPoint;
    }

    public static Crew Create(
        Guid id,
        Guid eventId,
        string name,
        string? description,
        int maxMembers,
        IEnumerable<string>? tags,
        MeetingPoint meetingPoint)
    {
        ThrowIfEmpty(id, nameof(id));
        ThrowIfEmpty(eventId, nameof(eventId));
        ArgumentNullException.ThrowIfNull(meetingPoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Trim().Length, MaxNameLength, nameof(name));
        ArgumentOutOfRangeException.ThrowIfLessThan(maxMembers, MinMembersLimit, nameof(maxMembers));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxMembers, MaxMembersLimit, nameof(maxMembers));

        var normalizedDescription = description?.Trim() ?? string.Empty;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            normalizedDescription.Length, MaxDescriptionLength, nameof(description));

        return new Crew(
            id, eventId, name.Trim(), normalizedDescription, maxMembers, NormalizeTags(tags), meetingPoint);
    }

    public JoinCrewResult Join(Guid userId)
    {
        ThrowIfEmpty(userId, nameof(userId));

        if (_members.Contains(userId))
        {
            return JoinCrewResult.AlreadyMember;
        }

        if (_members.Count >= MaxMembers)
        {
            return JoinCrewResult.Full;
        }

        _members.Add(userId);
        return JoinCrewResult.Joined;
    }

    /// <returns><c>true</c>, gdy uzytkownik byl czlonkiem i zostal usuniety.</returns>
    public bool Leave(Guid userId) => _members.Remove(userId);

    public bool HasMember(Guid userId) => _members.Contains(userId);

    private static List<string> NormalizeTags(IEnumerable<string>? tags)
    {
        var result = new List<string>();
        foreach (var tag in tags ?? [])
        {
            var normalized = NormalizeTag(tag);
            if (result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Duplicate tag '{normalized}'.", nameof(tags));
            }

            result.Add(normalized);
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(result.Count, MaxTags, nameof(tags));
        return result;
    }

    private static string NormalizeTag(string? tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        var normalized = tag.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(normalized.Length, MaxTagLength, nameof(tag));
        return normalized;
    }

    private static void ThrowIfEmpty(Guid value, string paramName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be an empty GUID.", paramName);
        }
    }
}
