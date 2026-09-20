using System.Net;
using System.Text.Json;
using FlowBB.Api.IntegrationTests.Endpoints;
using FlowBB.Api.IntegrationTests.Infrastructure;
using FlowBB.Application.Abstractions.AirQuality;
using FlowBB.Application.AirQuality;
using FlowBB.Domain.Common;
using FlowBB.Infrastructure.AirQuality;
using FluentAssertions;

namespace FlowBB.Api.IntegrationTests.Endpoints.AirQuality;

public sealed class AirQualityEndpointsTests
{
    [Fact]
    public async Task GetAirQuality_ReturnsContractResponseFromApplicationUseCase()
    {
        await using var host = await AirQualityTestHost.StartAsync(new FixedProvider());

        using var response = await host.Client.GetAsync(
            $"/api/events/{AirQualityTestHost.EventId}/air-quality");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        root.GetProperty("eventId").GetGuid().Should().Be(AirQualityTestHost.EventId);
        root.GetProperty("status").GetString().Should().Be("Fresh");
        root.GetProperty("source").GetString().Should().Be("Gios");
        root.GetProperty("pm25").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("no2").GetProperty("unit").GetString()
            .Should().Be(AirQualityMeasurement.CanonicalUnit);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task GetAirQuality_WithInvalidEventId_ReturnsBadRequestProblem(string eventId)
    {
        await using var host = await AirQualityTestHost.StartAsync(new FixedProvider());

        using var response = await host.Client.GetAsync($"/api/events/{eventId}/air-quality");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAirQuality_WithUnknownEvent_ReturnsNotFoundProblem()
    {
        await using var host = await AirQualityTestHost.StartAsync(new FixedProvider());

        using var response = await host.Client.GetAsync(
            "/api/events/99999999-9999-9999-9999-999999999999/air-quality");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAirQuality_WhenGiosFails_ReturnsDemoFallbackInsteadOfServerError()
    {
        await using var host = await AirQualityTestHost.StartAsync(
            new ThrowingProvider(),
            new DemoAirQualitySnapshotProvider());

        using var response = await host.Client.GetAsync(
            $"/api/events/{AirQualityTestHost.EventId}/air-quality");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("status").GetString().Should().Be("Fallback");
        json.RootElement.GetProperty("source").GetString().Should().Be("Demo");
    }

    [Fact]
    public async Task GetAirQuality_LogsNoEventOrResidentCoordinates()
    {
        await using var host = await AirQualityTestHost.StartAsync(new FixedProvider());

        using var response = await host.Client.GetAsync(
            $"/api/events/{AirQualityTestHost.EventId}/air-quality");

        response.EnsureSuccessStatusCode();
        var logs = string.Join('\n', host.Logs);
        logs.Should().NotContain("49.82245").And.NotContain("19.04431");
        logs.Should().NotContainEquivalentOf("originLatitude");
        logs.Should().NotContainEquivalentOf("userId");
    }

    private sealed class FixedProvider : IAirQualityProvider
    {
        public Task<AirQualityReading?> GetAsync(
            GeoPoint eventLocation,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AirQualityReading?>(new AirQualityReading(
                new AirQualityStation("Bielsko-Biała, ul. Kossak-Szczuckiej", 1576),
                DateTimeOffset.Parse("2026-09-20T10:00:00+02:00"),
                AirQualityLevel.Good,
                null,
                null,
                new AirQualityMeasurement(5.3),
                new AirQualityMeasurement(80.8)));
    }

    private sealed class ThrowingProvider : IAirQualityProvider
    {
        public Task<AirQualityReading?> GetAsync(
            GeoPoint eventLocation,
            CancellationToken cancellationToken = default) =>
            throw new AirQualityProviderException("Simulated GIOŚ outage.");
    }
}
