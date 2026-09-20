using FlowBB.Application.Abstractions.Routing;
using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowBB.Infrastructure.Routing;

/// <summary>
/// ROZWIAZANIE ZASTEPCZE MVP (DEMO DATA / SYMULACJA). Deterministyczny planer trasy: wynik zalezy wylacznie od
/// <see cref="RouteRequest"/> (bez losowosci, zegara systemowego, sieci, Neo4j i PostGIS). Obecne dane MZK nie zawieraja
/// pelnego routingu (brak kursow, kolejnosci przystankow i wspolrzednych), wiec nie sa tu uzywane.
/// Zalozenia demo:
/// <list type="bullet">
/// <item>czas przejazdu = odleglosc w linii prostej (haversine) / stala predkosc: pieszo 4,8, rower 15, samochod 30,
/// autobus 22 km/h, minimum 1 minuta na odcinek;</item>
/// <item>komunikacja miejska to zawsze to samo statyczne polaczenie: dojscie 6 min, oczekiwanie 5 min, wymyslona linia
/// "7 (demo)", dojscie 4 min (w powrocie odwrotnie). Nie ma rozkladu ani godzin kursowania, wiec
/// <c>ReturnGap</c> jest zawsze <c>false</c>;</item>
/// <item>przyjazd na wydarzenie 10 minut przed jego poczatkiem; powrot 10 minut po zakonczeniu, a dla komunikacji
/// miejskiej dodatkowo drugi o 40 minut po zakonczeniu;</item>
/// <item>gdy brak <c>EventEndAt</c>, wydarzenie trwa 2 godziny;</item>
/// <item>tryb <see cref="TransportMode.Unknown"/> jest odrzucany juz przez <see cref="RouteRequest"/>.</item>
/// </list>
/// </summary>
public sealed class DemoRoutePlanner : IRoutePlanner
{
    private const double WalkSpeedKmh = 4.8;
    private const double BikeSpeedKmh = 15;
    private const double CarSpeedKmh = 30;
    private const double TransitSpeedKmh = 22;
    private const int StopAccessMinutes = 6;
    private const int StopEgressMinutes = 4;
    private const int TransitWaitMinutes = 5;
    private const string DemoLine = "7 (demo)";

    private static readonly TimeSpan[] ReturnAdditionalOffsets = [TimeSpan.Zero, TimeSpan.FromMinutes(30)];

    private readonly ILogger<DemoRoutePlanner> _logger;

    public DemoRoutePlanner()
        : this(NullLogger<DemoRoutePlanner>.Instance)
    {
    }

    public DemoRoutePlanner(ILogger<DemoRoutePlanner> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<RoutePlan> PlanAsync(RouteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Demo route planner fallback used for event {EventId}.", request.EventId);

        var distanceKm = GeoDistance.KilometersBetween(request.Origin, request.Destination);
        var plan = new RoutePlan(
            PlannerSource.Demo,
            BuildOutbound(request, distanceKm),
            BuildReturns(request, distanceKm),
            returnGap: false);
        return Task.FromResult(plan);
    }

    private static JourneyOption BuildOutbound(RouteRequest request, double distanceKm)
    {
        var steps = BuildSteps(request.Mode, distanceKm, outbound: true);
        var duration = steps.Sum(step => step.DurationMinutes);
        var arrivalAt = RouteTiming.OutboundArrival(request.EventStartAt);
        return new JourneyOption(duration, arrivalAt.AddMinutes(-duration), arrivalAt, steps);
    }

    private static List<JourneyOption> BuildReturns(RouteRequest request, double distanceKm)
    {
        var firstDeparture = RouteTiming.FirstReturnDeparture(request.EventStartAt, request.EventEndAt);
        var steps = BuildSteps(request.Mode, distanceKm, outbound: false);
        var duration = steps.Sum(step => step.DurationMinutes);
        var offsets = request.Mode == TransportMode.PublicTransport
            ? ReturnAdditionalOffsets
            : ReturnAdditionalOffsets.Take(1);

        return offsets
            .Select(offset => firstDeparture + offset)
            .Select(departureAt => new JourneyOption(duration, departureAt, departureAt.AddMinutes(duration), steps))
            .ToList();
    }

    private static List<RouteStep> BuildSteps(TransportMode mode, double distanceKm, bool outbound) => mode switch
    {
        TransportMode.Walking =>
            [SingleLeg(RouteStepType.Walk, distanceKm, WalkSpeedKmh, outbound, "Idz pieszo", "Wroc pieszo")],
        TransportMode.Bike =>
            [SingleLeg(RouteStepType.Bike, distanceKm, BikeSpeedKmh, outbound, "Jedz rowerem", "Wroc rowerem")],
        TransportMode.Car =>
            [SingleLeg(RouteStepType.Car, distanceKm, CarSpeedKmh, outbound, "Jedz samochodem", "Wroc samochodem")],
        TransportMode.PublicTransport => BuildTransitSteps(distanceKm, outbound),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported transport mode.")
    };

    private static RouteStep SingleLeg(
        RouteStepType type, double distanceKm, double speedKmh, bool outbound, string outboundVerb, string returnVerb)
    {
        var minutes = MinutesFor(distanceKm, speedKmh);
        var instruction = outbound
            ? $"{outboundVerb} {minutes} min na miejsce wydarzenia."
            : $"{returnVerb} {minutes} min do punktu startu.";
        return new RouteStep(type, instruction, minutes);
    }

    private static List<RouteStep> BuildTransitSteps(double distanceKm, bool outbound)
    {
        var transitMinutes = MinutesFor(distanceKm, TransitSpeedKmh);
        var firstWalk = outbound ? StopAccessMinutes : StopEgressMinutes;
        var lastWalk = outbound ? StopEgressMinutes : StopAccessMinutes;
        var destination = outbound ? "na miejsce wydarzenia" : "do punktu startu";

        return
        [
            new RouteStep(RouteStepType.Walk, $"Idz {firstWalk} min do przystanku.", firstWalk),
            new RouteStep(RouteStepType.Wait, $"Poczekaj {TransitWaitMinutes} min na autobus.", TransitWaitMinutes),
            new RouteStep(
                RouteStepType.Transit,
                $"Jedz autobusem linii {DemoLine} ({transitMinutes} min).",
                transitMinutes,
                DemoLine),
            new RouteStep(RouteStepType.Walk, $"Idz {lastWalk} min {destination}.", lastWalk)
        ];
    }

    private static int MinutesFor(double distanceKm, double speedKmh) =>
        Math.Max(1, (int)Math.Ceiling(distanceKm / speedKmh * 60));
}
