namespace FlowBB.Application.Abstractions.AirQuality;

public interface IAirQualityCache
{
    /// <param name="lifetimeFor">Wybiera czas zycia wpisu na podstawie wyniku fabryki (np. krotki dla fallbacku).</param>
    Task<T> GetOrCreateAsync<T>(
        Guid eventId,
        Func<T, TimeSpan> lifetimeFor,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
        where T : class;
}
