using FlowBB.Application.Abstractions.Persistence;

namespace FlowBB.Application.Pulse.GetActivityMap;

public sealed class GetActivityMapHandler(IPulseDataReader reader)
{
    /// <summary>Minimalna liczba osob w komorce, ponizej ktorej komorka nie jest zwracana.</summary>
    public const int MinCellParticipants = 10;

    public async Task<ActivityMap> HandleAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event id cannot be an empty GUID.", nameof(eventId));
        }

        var points = await reader.GetPointsAsync(eventId, cancellationToken);
        return new ActivityMap(points.Count, ModalSplit.From(points), BuildCells(points));
    }

    private static List<HexCell> BuildCells(IReadOnlyList<PulsePoint> points) =>
        points
            .GroupBy(point => HexGrid.CellAt(point.Latitude, point.Longitude))
            .Where(group => group.Count() >= MinCellParticipants)
            .OrderBy(group => group.Key.Q)
            .ThenBy(group => group.Key.R)
            .Select(group => ToCell(group.Key, group.ToList()))
            .ToList();

    private static HexCell ToCell(HexCoordinate coordinate, List<PulsePoint> members) =>
        new(coordinate.Id, members.Count, ModalSplit.From(members), HexGrid.VerticesOf(coordinate));
}
