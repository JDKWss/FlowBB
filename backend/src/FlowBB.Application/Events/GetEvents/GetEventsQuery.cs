namespace FlowBB.Application.Events.GetEvents;

/// <summary>Opcjonalny zakres czasu dla <c>getEvents</c>. Zakres jest poprawny, gdy <c>From</c> nie jest pozniejsze niz <c>To</c>.</summary>
public sealed record GetEventsQuery(DateTimeOffset? From, DateTimeOffset? To)
{
    public bool HasValidRange() => From is null || To is null || From <= To;
}
