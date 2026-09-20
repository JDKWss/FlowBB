using FlowBB.Application.AirQuality;
using FlowBB.Domain.Common;
using FlowBB.Infrastructure.AirQuality;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace FlowBB.Api.IntegrationTests.AirQuality;

public sealed class AirQualityFallbackAndCacheTests
{
    private static readonly Guid GoldenEventId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task DemoProvider_LoadsEmbeddedSnapshotWithFallbackSemantics()
    {
        var provider = new DemoAirQualitySnapshotProvider();

        var result = await provider.GetAsync(
            GoldenEventId,
            new GeoPoint(49.82245, 19.04431));

        result.EventId.Should().Be(GoldenEventId);
        result.Status.Should().Be(AirQualityStatus.Fallback);
        result.Source.Should().Be(AirQualitySource.Demo);
        result.MeasuredAt.Should().Be(DateTimeOffset.Parse("2026-09-20T10:00:00+02:00"));
        result.Station.DistanceMeters.Should().Be(1576);
        result.Pm10.Should().BeNull();
        result.Pm25.Should().BeNull();
        result.No2!.Value.Should().Be(5.3);
        result.O3!.Value.Should().Be(80.8);
    }

    [Fact]
    public async Task MemoryCache_EntryWithShortLifetime_IsRefreshedAfterItExpires()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryAirQualityCache(memory);
        var calls = 0;
        Task<string> Factory(CancellationToken cancellationToken) => Task.FromResult($"call-{Interlocked.Increment(ref calls)}");

        var first = await cache.GetOrCreateAsync(GoldenEventId, _ => TimeSpan.FromMilliseconds(100), Factory);
        var withinWindow = await cache.GetOrCreateAsync(GoldenEventId, _ => TimeSpan.FromMilliseconds(100), Factory);
        await Task.Delay(300);
        var afterExpiry = await cache.GetOrCreateAsync(GoldenEventId, _ => TimeSpan.FromMilliseconds(100), Factory);

        (first, withinWindow, afterExpiry).Should().Be(("call-1", "call-1", "call-2"));
    }

    [Fact]
    public async Task MemoryCache_LifetimeIsChosenFromTheCreatedValue()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryAirQualityCache(memory);
        var calls = 0;
        Task<string> Factory(CancellationToken cancellationToken) => Task.FromResult($"call-{Interlocked.Increment(ref calls)}");
        static TimeSpan Lifetime(string value) => value == "call-1" ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromMinutes(5);

        await cache.GetOrCreateAsync(GoldenEventId, Lifetime, Factory);
        await Task.Delay(300);
        var second = await cache.GetOrCreateAsync(GoldenEventId, Lifetime, Factory);
        var third = await cache.GetOrCreateAsync(GoldenEventId, Lifetime, Factory);

        (second, third).Should().Be(("call-2", "call-2"));
    }

    [Fact]
    public async Task MemoryCache_ConcurrentRequests_RunFactoryOnce()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryAirQualityCache(memory);
        var calls = 0;

        async Task<string> Factory(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(20, cancellationToken);
            return "cached";
        }

        var results = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ =>
                cache.GetOrCreateAsync(
                    GoldenEventId,
                    _ => TimeSpan.FromMinutes(20),
                    Factory)));

        results.Should().OnlyContain(value => value == "cached");
        calls.Should().Be(1);
    }
}
