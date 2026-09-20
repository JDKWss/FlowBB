using System.Collections.Concurrent;
using FlowBB.Application.Abstractions.AirQuality;
using Microsoft.Extensions.Caching.Memory;

namespace FlowBB.Infrastructure.AirQuality;

public sealed class MemoryAirQualityCache(IMemoryCache cache) : IAirQualityCache
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gates = new();

    public async Task<T> GetOrCreateAsync<T>(
        Guid eventId,
        TimeSpan lifetime,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (cache.TryGetValue<T>(eventId, out var cached) && cached is not null)
        {
            return cached;
        }

        var gate = _gates.GetOrAdd(eventId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue<T>(eventId, out cached) && cached is not null)
            {
                return cached;
            }

            var created = await factory(cancellationToken);
            cache.Set(eventId, created, lifetime);
            return created;
        }
        finally
        {
            gate.Release();
        }
    }
}
