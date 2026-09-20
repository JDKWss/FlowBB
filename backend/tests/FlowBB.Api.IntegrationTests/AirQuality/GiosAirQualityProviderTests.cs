using System.Net;
using System.Text;
using FlowBB.Application.Abstractions.AirQuality;
using FlowBB.Domain.Common;
using FlowBB.Infrastructure.AirQuality;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowBB.Api.IntegrationTests.AirQuality;

public sealed class GiosAirQualityProviderTests
{
    private static readonly GeoPoint EventLocation = new(49.82245, 19.04431);

    [Fact]
    public async Task GetAsync_MapsOfficialV1ResponsesWithoutInternet()
    {
        using var httpClient = ClientReturning(ValidResponses());
        var provider = Provider(httpClient);

        var result = await provider.GetAsync(EventLocation);

        result.Should().NotBeNull();
        result!.Station.Name.Should().Be("Bielsko-Biała, ul. Kossak-Szczuckiej");
        result.Station.DistanceMeters.Should().Be(1576);
        result.MeasuredAt.Should().Be(DateTimeOffset.Parse("2026-09-20T10:00:00+02:00"));
        result.QualityLevel.Should().Be(Application.AirQuality.AirQualityLevel.Good);
        result.Pm10.Should().BeNull();
        result.Pm25.Should().BeNull();
        result.No2!.Value.Should().Be(5.3);
        result.O3!.Value.Should().Be(80.8);
    }

    [Fact]
    public async Task GetAsync_ReusesStationAndSensorMetadataAcrossEvents()
    {
        var handler = new StubHandler(ValidResponses());
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.gios.gov.pl/pjp-api/")
        };
        var provider = Provider(httpClient);

        await provider.GetAsync(EventLocation);
        await provider.GetAsync(new GeoPoint(49.82055, 19.04863));

        handler.Calls["/pjp-api/v1/rest/station/findAll?page=0&size=500"].Should().Be(1);
        handler.Calls["/pjp-api/v1/rest/station/sensors/789"].Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_WhenGiosReturnsHttpError_ThrowsProviderException()
    {
        using var httpClient = ClientReturning(new Dictionary<string, StubResponse>
        {
            ["/pjp-api/v1/rest/station/findAll?page=0&size=500"] =
                new(HttpStatusCode.ServiceUnavailable, """{"message":"unavailable"}""")
        });
        var provider = Provider(httpClient);

        var action = () => provider.GetAsync(EventLocation);

        await action.Should().ThrowAsync<AirQualityProviderException>()
            .WithMessage("*HTTP status 503*");
    }

    [Fact]
    public async Task GetAsync_WhenGiosPayloadIsMalformed_ThrowsProviderException()
    {
        using var httpClient = ClientReturning(new Dictionary<string, StubResponse>
        {
            ["/pjp-api/v1/rest/station/findAll?page=0&size=500"] =
                new(HttpStatusCode.OK, "not-json")
        });
        var provider = Provider(httpClient);

        var action = () => provider.GetAsync(EventLocation);

        await action.Should().ThrowAsync<AirQualityProviderException>()
            .WithMessage("*invalid response*");
    }

    private static HttpClient ClientReturning(IReadOnlyDictionary<string, StubResponse> responses) => new(
        new StubHandler(responses))
    {
        BaseAddress = new Uri("https://api.gios.gov.pl/pjp-api/")
    };

    private static GiosAirQualityProvider Provider(HttpClient httpClient) => new(
        httpClient,
        new MemoryCache(new MemoryCacheOptions()),
        NullLogger<GiosAirQualityProvider>.Instance);

    private static IReadOnlyDictionary<string, StubResponse> ValidResponses() =>
        new Dictionary<string, StubResponse>
        {
            ["/pjp-api/v1/rest/station/findAll?page=0&size=500"] = new(HttpStatusCode.OK, """
                {
                  "Lista stacji pomiarowych": [{
                    "Identyfikator stacji": 789,
                    "Nazwa stacji": "Bielsko-Biała, ul. Kossak-Szczuckiej",
                    "WGS84 φ N": "49.813464",
                    "WGS84 λ E": "19.027318"
                  }]
                }
                """),
            ["/pjp-api/v1/rest/station/sensors/789"] = new(HttpStatusCode.OK, """
                {
                  "Lista stanowisk pomiarowych dla podanej stacji": [
                    {"Identyfikator stanowiska":5167,"Wskaźnik - kod":"PM10"},
                    {"Identyfikator stanowiska":5162,"Wskaźnik - kod":"NO2"},
                    {"Identyfikator stanowiska":5164,"Wskaźnik - kod":"O3"}
                  ]
                }
                """),
            ["/pjp-api/v1/rest/data/getData/5167"] = new(HttpStatusCode.OK, """
                {"Lista danych pomiarowych":[{"Data":"2026-09-20 10:00:00","Wartość":null}]}
                """),
            ["/pjp-api/v1/rest/data/getData/5162"] = new(HttpStatusCode.OK, """
                {"Lista danych pomiarowych":[{"Data":"2026-09-20 10:00:00","Wartość":5.3}]}
                """),
            ["/pjp-api/v1/rest/data/getData/5164"] = new(HttpStatusCode.OK, """
                {"Lista danych pomiarowych":[{"Data":"2026-09-20 10:00:00","Wartość":80.8}]}
                """),
            ["/pjp-api/v1/rest/aqindex/getIndex/789"] = new(HttpStatusCode.OK, """
                {"AqIndex":{"Nazwa kategorii indeksu":"Dobry"}}
                """)
        };

    private sealed record StubResponse(HttpStatusCode StatusCode, string Json);

    private sealed class StubHandler(IReadOnlyDictionary<string, StubResponse> responses) : HttpMessageHandler
    {
        public Dictionary<string, int> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;
            Calls[path] = Calls.GetValueOrDefault(path) + 1;
            if (!responses.TryGetValue(path, out var stub))
            {
                throw new InvalidOperationException($"Unexpected HTTP request: {path}");
            }

            return Task.FromResult(new HttpResponseMessage(stub.StatusCode)
            {
                Content = new StringContent(stub.Json, Encoding.UTF8, "application/json")
            });
        }
    }
}
