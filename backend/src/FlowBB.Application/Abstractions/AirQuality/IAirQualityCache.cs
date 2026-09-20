namespace FlowBB.Application.Abstractions.AirQuality;

public interface IAirQualityCache
{
    Task<T> GetOrCreateAsync<T>(
        Guid eventId,
        TimeSpan lifetime,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
        where T : class;
}
