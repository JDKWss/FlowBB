namespace FlowBB.Infrastructure.Routing;

internal static class RouteTiming
{
    private static readonly TimeSpan ArrivalBuffer = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DefaultEventDuration = TimeSpan.FromHours(2);
    private static readonly TimeSpan ReturnOffset = TimeSpan.FromMinutes(10);

    public static DateTimeOffset OutboundArrival(DateTimeOffset eventStartAt) => eventStartAt - ArrivalBuffer;

    public static DateTimeOffset FirstReturnDeparture(DateTimeOffset eventStartAt, DateTimeOffset? eventEndAt) =>
        (eventEndAt ?? eventStartAt + DefaultEventDuration) + ReturnOffset;
}
