using FlowBB.Application.Abstractions.AirQuality;
using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Domain.Common;

namespace FlowBB.Application.AirQuality.GetEventAirQuality;

public sealed record AirQualityPolicyOptions
{
    /// <summary>Domyslny krotki czas cache dla wyniku Fallback, zeby po awarii GIOS szybko wrocic do zrodla.</summary>
    public static readonly TimeSpan DefaultFallbackCacheLifetime = TimeSpan.FromSeconds(60);

    public AirQualityPolicyOptions(
        TimeSpan cacheLifetime,
        TimeSpan sourceTimeout,
        TimeSpan freshnessThreshold,
        TimeSpan? fallbackCacheLifetime = null)
    {
        CacheLifetime = RequirePositive(cacheLifetime, nameof(cacheLifetime));
        FallbackCacheLifetime = RequirePositive(fallbackCacheLifetime ?? DefaultFallbackCacheLifetime, nameof(fallbackCacheLifetime));
        SourceTimeout = RequirePositive(sourceTimeout, nameof(sourceTimeout));
        FreshnessThreshold = RequirePositive(freshnessThreshold, nameof(freshnessThreshold));
    }

    public TimeSpan CacheLifetime { get; }

    public TimeSpan FallbackCacheLifetime { get; }

    public TimeSpan SourceTimeout { get; }

    public TimeSpan FreshnessThreshold { get; }

    private static TimeSpan RequirePositive(TimeSpan value, string parameterName) =>
        value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(parameterName, value, "Duration must be greater than zero.");
}

public sealed class GetEventAirQualityHandler(
    IEventLookup events,
    IAirQualityProvider provider,
    IAirQualityFallbackProvider fallback,
    IAirQualityCache cache,
    TimeProvider clock,
    AirQualityPolicyOptions options)
{
    public async Task<EventAirQuality?> HandleAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event id must not be empty.", nameof(eventId));
        }

        var @event = await events.FindByIdAsync(eventId, cancellationToken);
        if (@event is null)
        {
            return null;
        }

        return await cache.GetOrCreateAsync(
            eventId,
            LifetimeFor,
            token => LoadAsync(eventId, @event.Location, token),
            cancellationToken);
    }

    private TimeSpan LifetimeFor(EventAirQuality result) =>
        result.Status == AirQualityStatus.Fallback ? options.FallbackCacheLifetime : options.CacheLifetime;

    private async Task<EventAirQuality> LoadAsync(
        Guid eventId,
        GeoPoint eventLocation,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.SourceTimeout);

        try
        {
            var reading = await provider.GetAsync(eventLocation, timeout.Token);
            return reading is null
                ? await fallback.GetAsync(eventId, eventLocation, cancellationToken)
                : FromGios(eventId, reading);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await fallback.GetAsync(eventId, eventLocation, cancellationToken);
        }
        catch (AirQualityProviderException)
        {
            return await fallback.GetAsync(eventId, eventLocation, cancellationToken);
        }
    }

    private EventAirQuality FromGios(Guid eventId, AirQualityReading reading)
    {
        var age = clock.GetUtcNow() - reading.MeasuredAt.ToUniversalTime();
        var status = age <= options.FreshnessThreshold
            ? AirQualityStatus.Fresh
            : AirQualityStatus.Stale;

        return new EventAirQuality(
            eventId,
            reading.Station,
            reading.MeasuredAt,
            reading.QualityLevel,
            status,
            AirQualitySource.Gios,
            reading.Pm10,
            reading.Pm25,
            reading.No2,
            reading.O3,
            Alert: null);
    }
}
