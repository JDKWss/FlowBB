using System.Net;
using System.Text.Json;
using FlowBB.Api.IntegrationTests.Endpoints;
using FluentAssertions;

namespace FlowBB.Api.IntegrationTests.Smoke;

[Collection(SmokeCollection.Name)]
public sealed class RoutingSmokeTests : SmokeTestBase
{
    private static string RoutePath(Guid eventId, Guid userId) => $"/api/events/{eventId}/route?userId={userId}";

    [SmokeTheory]
    [InlineData("Walking")]
    [InlineData("PublicTransport")]
    [InlineData("Bike")]
    [InlineData("Car")]
    public async Task Route_ForADeclaredAttendance_IsTheSamePlanEveryTimeWithAConsistentShape(string mode)
    {
        (await Api.DeclareAsync(SmokeSeed.Run, SmokeSeed.FreeUser, mode)).Dispose();

        using var first = await Api.GetAsync(RoutePath(SmokeSeed.Run, SmokeSeed.FreeUser));
        var firstBody = await first.Content.ReadAsStringAsync();
        using var second = await Api.GetAsync(RoutePath(SmokeSeed.Run, SmokeSeed.FreeUser));
        var plan = await SmokeClient.ReadAsync(first);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertPlannerSource(plan.GetProperty("plannerSource").GetString(), mode);
        plan.GetProperty("outbound").GetProperty("steps").GetArrayLength().Should().BeGreaterThan(0);
        AssertReturnGap(plan, mode);
        AssertGeometryMatchesTheSource(plan);
        (await second.Content.ReadAsStringAsync()).Should().Be(firstBody, "the planner is deterministic for the same data");
    }

    // PublicTransport zawsze idzie przez planer demo. Pozostale tryby uzywaja planera drogowego, gdy usluga routingu ma
    // przygotowane grafy (profil real-routing), a w przeciwnym razie kontrolowanego fallbacku demo.
    private static void AssertPlannerSource(string? source, string mode)
    {
        if (mode == "PublicTransport")
        {
            source.Should().Be("Demo");
            return;
        }

        source.Should().BeOneOf("Demo", "RoadRouting");
    }

    // Wydarzenie Run konczy sie o 23:15 (Europe/Warsaw), wiec DemoReturnGapPolicy uznaje, ze uczestnik PublicTransport nie ma
    // dogodnego powrotu: returnGap true i pusta lista returns. Pozostale tryby zachowuja powroty z planera.
    private static void AssertReturnGap(JsonElement plan, string mode)
    {
        var expectGap = mode == "PublicTransport";
        plan.GetProperty("returnGap").GetBoolean().Should().Be(expectGap);
        if (expectGap)
        {
            plan.GetProperty("returns").GetArrayLength().Should().Be(0);
            return;
        }

        plan.GetProperty("returns").GetArrayLength().Should().BeGreaterThan(0);
    }

    // RoadRouting niesie geometrie LineString i dystans; plan demo nie ma ani jednego, ani drugiego.
    private static void AssertGeometryMatchesTheSource(JsonElement plan)
    {
        var outbound = plan.GetProperty("outbound");
        var hasGeometry = outbound.TryGetProperty("geometry", out var geometry) && geometry.ValueKind == JsonValueKind.Object;
        if (plan.GetProperty("plannerSource").GetString() == "RoadRouting")
        {
            hasGeometry.Should().BeTrue("a road route carries its geometry");
            geometry.GetProperty("type").GetString().Should().Be("LineString");
            geometry.GetProperty("coordinates").GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
            outbound.GetProperty("distanceMeters").GetDouble().Should().BeGreaterThan(0);
            return;
        }

        hasGeometry.Should().BeFalse("the demo planner has no geometry");
    }

    [SmokeFact]
    public async Task Route_WithoutADeclaredAttendance_Returns404()
    {
        using var response = await Api.GetAsync(RoutePath(SmokeSeed.Run, SmokeSeed.FreeUser));

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.NotFound);
    }

    [SmokeFact]
    public async Task Route_WithUnknownEvent_Returns404()
    {
        using var response = await Api.GetAsync(RoutePath(SmokeSeed.Unknown, SmokeSeed.FreeUser));

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.NotFound);
    }

    [SmokeFact]
    public async Task Route_ForAnUnknownTransportMode_Returns400()
    {
        (await Api.DeclareAsync(SmokeSeed.Run, SmokeSeed.FreeUser, "Unknown")).Dispose();

        using var response = await Api.GetAsync(RoutePath(SmokeSeed.Run, SmokeSeed.FreeUser));

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }

    [SmokeTheory]
    [InlineData("/api/events/33333333-3333-3333-3333-333333333333/route")]
    [InlineData("/api/events/33333333-3333-3333-3333-333333333333/route?userId=not-a-guid")]
    [InlineData("/api/events/not-a-guid/route?userId=d1000000-0000-0000-0000-000000000082")]
    public async Task Route_WithMissingOrInvalidIds_Returns400(string path)
    {
        using var response = await Api.GetAsync(path);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }
}
