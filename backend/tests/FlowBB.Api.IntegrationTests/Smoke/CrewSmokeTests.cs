using System.Net;
using System.Text.Json;
using FlowBB.Api.IntegrationTests.Endpoints;
using FluentAssertions;

namespace FlowBB.Api.IntegrationTests.Smoke;

[Collection(SmokeCollection.Name)]
public sealed class CrewSmokeTests : SmokeTestBase
{
    private static string GroupsPath(Guid eventId) => $"/api/events/{eventId}/groups?userId={SmokeSeed.FreeUser}";

    private async Task<JsonElement> GroupAsync(Guid crewId)
    {
        var groups = await Api.GetJsonAsync(GroupsPath(SmokeSeed.Concert));
        return groups.EnumerateArray().Single(group => group.GetProperty("id").GetGuid() == crewId);
    }

    [SmokeFact]
    public async Task Groups_ListTheEventCrewsWithoutMemberIdentifiers()
    {
        using var response = await Api.GetAsync(GroupsPath(SmokeSeed.Concert));
        var raw = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(raw);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        raw.Should().NotContainEquivalentOf("userId");
        var groups = document.RootElement.EnumerateArray().ToList();
        groups.Should().Contain(group => group.GetProperty("id").GetGuid() == SmokeSeed.OpenCrew);
        groups.Should().OnlyContain(group => group.GetProperty("currentMembers").GetInt32() <= group.GetProperty("maxMembers").GetInt32());
        groups.Should().OnlyContain(group => !group.GetProperty("joinedByCurrentUser").GetBoolean());
    }

    [SmokeFact]
    public async Task Groups_WithUnknownEvent_Returns404()
    {
        using var response = await Api.GetAsync($"/api/events/{SmokeSeed.Unknown}/groups");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.NotFound);
    }

    [SmokeTheory]
    [InlineData("/api/events/not-a-guid/groups")]
    [InlineData("/api/events/11111111-1111-1111-1111-111111111111/groups?userId=not-a-guid")]
    public async Task Groups_WithInvalidIds_Returns400(string path)
    {
        using var response = await Api.GetAsync(path);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }

    [SmokeFact]
    public async Task Join_AddsTheMemberOnceAndLeaveIsIdempotent()
    {
        var before = (await GroupAsync(SmokeSeed.OpenCrew)).GetProperty("currentMembers").GetInt32();

        using var first = await Api.JoinAsync(SmokeSeed.OpenCrew, SmokeSeed.FreeUser);
        var joined = await SmokeClient.ReadAsync(first);
        using var repeated = await Api.JoinAsync(SmokeSeed.OpenCrew, SmokeSeed.FreeUser);
        var again = await SmokeClient.ReadAsync(repeated);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        joined.GetProperty("currentMembers").GetInt32().Should().Be(before + 1);
        joined.GetProperty("joinedByCurrentUser").GetBoolean().Should().BeTrue();
        again.GetProperty("currentMembers").GetInt32().Should().Be(before + 1);

        using var left = await Api.LeaveAsync(SmokeSeed.OpenCrew, SmokeSeed.FreeUser);
        using var leftAgain = await Api.LeaveAsync(SmokeSeed.OpenCrew, SmokeSeed.FreeUser);
        left.StatusCode.Should().Be(HttpStatusCode.NoContent);
        leftAgain.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GroupAsync(SmokeSeed.OpenCrew)).GetProperty("currentMembers").GetInt32().Should().Be(before);
    }

    [SmokeFact]
    public async Task Join_AFullCrew_Returns409AndDoesNotChangeIt()
    {
        var before = (await GroupAsync(SmokeSeed.FullCrew)).GetProperty("currentMembers").GetInt32();

        using var response = await Api.JoinAsync(SmokeSeed.FullCrew, SmokeSeed.FreeUser);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.Conflict);
        (await GroupAsync(SmokeSeed.FullCrew)).GetProperty("currentMembers").GetInt32().Should().Be(before);
    }

    [SmokeFact]
    public async Task Join_WithUnknownCrew_Returns404()
    {
        using var response = await Api.JoinAsync(SmokeSeed.Unknown, SmokeSeed.FreeUser);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.NotFound);
    }

    [SmokeFact]
    public async Task Join_WithUnknownUser_Returns404()
    {
        using var response = await Api.JoinAsync(SmokeSeed.OpenCrew, SmokeSeed.Unknown);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.NotFound);
    }

    [SmokeTheory]
    [InlineData("{}", "application/json")]
    [InlineData("{\"userId\":\"not-a-guid\"}", "application/json")]
    [InlineData("not json", "application/json")]
    [InlineData("{}", "text/plain")]
    public async Task Join_WithInvalidRequest_Returns400(string body, string contentType)
    {
        using var response = await Api.PostRawAsync($"/api/groups/{SmokeSeed.OpenCrew}/members", body, contentType);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }

    [SmokeTheory]
    [InlineData("not-a-guid", "d1000000-0000-0000-0000-000000000082")]
    [InlineData("22222222-2222-2222-2222-222222222222", "not-a-guid")]
    public async Task Leave_WithInvalidIds_Returns400(string crewId, string userId)
    {
        using var response = await Api.DeleteAsync($"/api/groups/{crewId}/members/{userId}");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }
}
