using FlowBB.Application.AirQuality;
using FlowBB.Domain.Common;

namespace FlowBB.Application.Abstractions.AirQuality;

/// <summary>
/// Supplies normalized air-quality data for an event location.
/// Implementations must not depend on resident, attendance, route or crew data.
/// </summary>
public interface IAirQualityProvider
{
    Task<AirQualityReading?> GetAsync(
        GeoPoint eventLocation,
        CancellationToken cancellationToken = default);
}
