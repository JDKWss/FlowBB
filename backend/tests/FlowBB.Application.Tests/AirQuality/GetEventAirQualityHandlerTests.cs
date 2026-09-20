using System.Collections.Concurrent;
using FlowBB.Application.Abstractions.AirQuality;
using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.AirQuality;
using FlowBB.Application.AirQuality.GetEventAirQuality;
using FlowBB.Domain.Common;
using FlowBB.Domain.Events;
using FluentAssertions;
using Moq;

namespace FlowBB.Application.Tests.AirQuality;

public sealed class GetEventAirQualityHandlerTests
{
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-20T10:30:00+02:00");
    private static readonly GeoPoint EventLocation = new(49.82245, 19.04431);

    [Fact]
    public async Task HandleAsync_WithRecentProviderReading_ReturnsFreshGiosResult()
    {
        var provider = ProviderReturning(Reading(Now.AddMinutes(-30)));
        var fallback = new Mock<IAirQualityFallbackProvider>(MockBehavior.Strict);
        var handler = Handler(provider.Object, fallback.Object);

        var result = await handler.HandleAsync(EventId);

        result.Should().NotBeNull();
        result!.Status.Should().Be(AirQualityStatus.Fresh);
        result.Source.Should().Be(AirQualitySource.Gios);
        fallback.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WithOldProviderReading_ReturnsStaleGiosResult()
    {
        var handler = Handler(
            ProviderReturning(Reading(Now.AddMinutes(-91))).Object,
            Mock.Of<IAirQualityFallbackProvider>());

        var result = await handler.HandleAsync(EventId);

        result!.Status.Should().Be(AirQualityStatus.Stale);
        result.Source.Should().Be(AirQualitySource.Gios);
    }

    [Fact]
    public async Task HandleAsync_WhenProviderThrows_ReturnsDemoFallback()
    {
        var provider = new Mock<IAirQualityProvider>();
        provider.Setup(item => item.GetAsync(EventLocation, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AirQualityProviderException("Unavailable."));
        var fallback = Fallback();
        var handler = Handler(provider.Object, fallback.Object);

        var result = await handler.HandleAsync(EventId);

        result.Should().BeSameAs(FallbackResult);
        fallback.Verify(item => item.GetAsync(EventId, EventLocation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenProviderTimesOut_ReturnsDemoFallback()
    {
        var provider = new Mock<IAirQualityProvider>();
        provider.Setup(item => item.GetAsync(EventLocation, It.IsAny<CancellationToken>()))
            .Returns(async (GeoPoint _, CancellationToken token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return null;
            });
        var handler = Handler(provider.Object, Fallback().Object, sourceTimeout: TimeSpan.FromMilliseconds(20));

        var result = await handler.HandleAsync(EventId);

        result.Should().BeSameAs(FallbackResult);
    }

    [Theory]
    [InlineData(-30)]
    [InlineData(-91)]
    public async Task HandleAsync_SuccessfulGiosReading_IsCachedForTheFullCacheLifetime(int measuredMinutesAgo)
    {
        var cache = new TestAirQualityCache();
        var handler = Handler(ProviderReturning(Reading(Now.AddMinutes(measuredMinutesAgo))).Object, Mock.Of<IAirQualityFallbackProvider>(), cache: cache);

        await handler.HandleAsync(EventId);

        cache.LastLifetime.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task HandleAsync_FallbackResult_IsCachedOnlyForTheShortFallbackLifetime()
    {
        var provider = new Mock<IAirQualityProvider>();
        provider.Setup(item => item.GetAsync(EventLocation, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AirQualityProviderException("Unavailable."));
        var cache = new TestAirQualityCache();
        var handler = Handler(provider.Object, Fallback().Object, cache: cache);

        await handler.HandleAsync(EventId);

        cache.LastLifetime.Should().Be(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public async Task HandleAsync_WithinCacheWindow_CallsProviderOnce()
    {
        var provider = ProviderReturning(Reading(Now));
        var handler = Handler(provider.Object, Mock.Of<IAirQualityFallbackProvider>());

        var first = await handler.HandleAsync(EventId);
        var second = await handler.HandleAsync(EventId);

        second.Should().BeSameAs(first);
        provider.Verify(item => item.GetAsync(EventLocation, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static GetEventAirQualityHandler Handler(
        IAirQualityProvider provider,
        IAirQualityFallbackProvider fallback,
        TimeSpan? sourceTimeout = null,
        TestAirQualityCache? cache = null)
    {
        var events = new Mock<IEventLookup>();
        events.Setup(item => item.FindByIdAsync(EventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DemoEvent());
        return new GetEventAirQualityHandler(
            events.Object,
            provider,
            fallback,
            cache ?? new TestAirQualityCache(),
            new FixedTimeProvider(Now),
            new AirQualityPolicyOptions(
                TimeSpan.FromMinutes(30),
                sourceTimeout ?? TimeSpan.FromSeconds(1),
                TimeSpan.FromMinutes(90),
                TimeSpan.FromSeconds(45)));
    }

    private static Mock<IAirQualityProvider> ProviderReturning(AirQualityReading reading)
    {
        var provider = new Mock<IAirQualityProvider>();
        provider.Setup(item => item.GetAsync(EventLocation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reading);
        return provider;
    }

    private static Mock<IAirQualityFallbackProvider> Fallback()
    {
        var fallback = new Mock<IAirQualityFallbackProvider>();
        fallback.Setup(item => item.GetAsync(EventId, EventLocation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FallbackResult);
        return fallback;
    }

    private static AirQualityReading Reading(DateTimeOffset measuredAt) => new(
        new AirQualityStation("Bielsko-Biała, ul. Kossak-Szczuckiej", 1576),
        measuredAt,
        AirQualityLevel.Good,
        new AirQualityMeasurement(10.4),
        null,
        new AirQualityMeasurement(6.6),
        new AirQualityMeasurement(64.9));

    private static Event DemoEvent() => new(
        EventId,
        "Koncert na Rynku",
        "Demo",
        Now.AddHours(8),
        Now.AddHours(10),
        "Rynek",
        EventCategory.Culture,
        EventSource.Demo,
        EventLocation);

    private static readonly EventAirQuality FallbackResult = new(
        EventId,
        new AirQualityStation("Bielsko-Biała, ul. Kossak-Szczuckiej", 1576),
        DateTimeOffset.Parse("2026-09-20T10:00:00+02:00"),
        AirQualityLevel.Good,
        AirQualityStatus.Fallback,
        AirQualitySource.Demo,
        null,
        null,
        new AirQualityMeasurement(5.3),
        new AirQualityMeasurement(80.8),
        null);

    private sealed class TestAirQualityCache : IAirQualityCache
    {
        private readonly ConcurrentDictionary<Guid, object> _items = new();

        public TimeSpan? LastLifetime { get; private set; }

        public async Task<T> GetOrCreateAsync<T>(
            Guid eventId,
            Func<T, TimeSpan> lifetimeFor,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken cancellationToken = default)
            where T : class
        {
            if (_items.TryGetValue(eventId, out var cached))
            {
                return (T)cached;
            }

            var created = await factory(cancellationToken);
            _items[eventId] = created;
            LastLifetime = lifetimeFor(created);
            return created;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
