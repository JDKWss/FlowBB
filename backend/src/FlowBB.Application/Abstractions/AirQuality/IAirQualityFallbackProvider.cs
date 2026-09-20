using FlowBB.Application.AirQuality;
using FlowBB.Domain.Common;

namespace FlowBB.Application.Abstractions.AirQuality;

public interface IAirQualityFallbackProvider
{
    Task<EventAirQuality> GetAsync(
        Guid eventId,
        GeoPoint eventLocation,
        CancellationToken cancellationToken = default);
}
