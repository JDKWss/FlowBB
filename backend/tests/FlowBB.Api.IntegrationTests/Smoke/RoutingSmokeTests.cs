using System.Net;
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
    public async Task Route_ForADeclaredAttendance_IsTheSameDemoPlanEveryTime(string mode)
    {
        (await Api.DeclareAsync(SmokeSeed.Run, SmokeSeed.FreeUser, mode)).Dispose();

        using var first = await Api.GetAsync(RoutePath(SmokeSeed.Run, SmokeSeed.FreeUser));
        var firstBody = await first.Content.ReadAsStringAsync();
        using var second = await Api.GetAsync(RoutePath(SmokeSeed.Run, SmokeSeed.FreeUser));
        var plan = await SmokeClient.ReadAsync(first);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        plan.GetProperty("plannerSource").GetString().Should().Be("Demo");
        plan.GetProperty("outbound").GetProperty("steps").GetArrayLength().Should().BeGreaterThan(0);
        plan.GetProperty("returns").GetArrayLength().Should().BeGreaterThan(0);
        (await second.Content.ReadAsStringAsync()).Should().Be(firstBody, "the demo planner is deterministic");
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
