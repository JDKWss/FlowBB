namespace FlowBB.Domain.Routing;

/// <summary>Trasa na wydarzenie i powrot. <c>ReturnGap</c> oznacza, ze po wydarzeniu nie ma dogodnego powrotu.</summary>
public sealed record RoutePlan
{
    public RoutePlan(
        PlannerSource source,
        JourneyOption outbound,
        IReadOnlyList<JourneyOption> returns,
        bool returnGap)
    {
        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown planner source.");
        }

        ArgumentNullException.ThrowIfNull(outbound);
        ArgumentNullException.ThrowIfNull(returns);

        Source = source;
        Outbound = outbound;
        Returns = returns;
        ReturnGap = returnGap;
    }

    public PlannerSource Source { get; }

    public JourneyOption Outbound { get; }

    public IReadOnlyList<JourneyOption> Returns { get; }

    public bool ReturnGap { get; }
}
