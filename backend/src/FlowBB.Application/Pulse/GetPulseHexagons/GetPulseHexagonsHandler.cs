using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Pulse.GetActivityMap;

namespace FlowBB.Application.Pulse.GetPulseHexagons;

/// <summary>Komorki heksagonalne wydarzenia (tylko z co najmniej 10 osobami) z kontrola istnienia wydarzenia.</summary>
public sealed class GetPulseHexagonsHandler(IPulseDataReader reader, GetActivityMapHandler activityMap)
{
    /// <returns>Komorki lub <c>null</c>, gdy wydarzenie nie istnieje.</returns>
    public async Task<IReadOnlyList<HexCell>?> HandleAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event id cannot be an empty GUID.", nameof(eventId));
        }

        if (await reader.GetEventAsync(eventId, cancellationToken) is null)
        {
            return null;
        }

        var map = await activityMap.HandleAsync(eventId, cancellationToken);
        return map.Cells;
    }
}
