using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowBB.Api.IntegrationTests.Endpoints;
using FlowBB.Api.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace FlowBB.Api.IntegrationTests.Endpoints.Crews;

public class CrewsEndpointsTests
{
    private static string GroupsUrl(Guid eventId, string? userId = null) =>
        $"/api/events/{eventId}/groups" + (userId is null ? string.Empty : $"?userId={userId}");

    private static string MembersUrl(Guid crewId) => $"/api/groups/{crewId}/members";

    private static Task<HttpResponseMessage> JoinAsync(CrewsTestHost host, Guid crewId, Guid userId) =>
        host.Client.PostAsJsonAsync(MembersUrl(crewId), new { userId });

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    [Fact]
    public async Task GetGroups_ReturnsGroupsMatchingContract()
    {
        await using var host = await CrewsTestHost.StartAsync();

        using var response = await host.Client.GetAsync(GroupsUrl(FakeCrewRepository.EventId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetArrayLength().Should().Be(2);
        var first = document.RootElement[0];
        first.GetProperty("id").GetGuid().Should().Be(FakeCrewRepository.NewcomersCrewId);
        first.GetProperty("eventId").GetGuid().Should().Be(FakeCrewRepository.EventId);
        first.GetProperty("currentMembers").GetInt32().Should().Be(0);
        first.GetProperty("maxMembers").GetInt32().Should().Be(6);
        first.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).Should().Equal("muzyka");
        first.GetProperty("meetingPoint").GetProperty("name").GetString().Should().Be("Plac Chrobrego");
        first.GetProperty("joinedByCurrentUser").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task GetGroups_MarksGroupJoinedByCurrentUserOnly()
    {
        await using var host = await CrewsTestHost.StartAsync();
        host.Crews.Seed(FakeCrewRepository.CyclistsCrewId, FakeCrewRepository.UserA);

        var asMember = await host.Client.GetStringAsync(GroupsUrl(FakeCrewRepository.EventId, FakeCrewRepository.UserA.ToString()));
        var asOther = await host.Client.GetStringAsync(GroupsUrl(FakeCrewRepository.EventId, FakeCrewRepository.UserB.ToString()));

        using var memberView = JsonDocument.Parse(asMember);
        memberView.RootElement.EnumerateArray().Select(group => group.GetProperty("joinedByCurrentUser").GetBoolean())
            .Should().Equal(false, true);
        using var otherView = JsonDocument.Parse(asOther);
        otherView.RootElement.EnumerateArray().Should().OnlyContain(group => !group.GetProperty("joinedByCurrentUser").GetBoolean());
    }

    [Fact]
    public async Task GetGroups_DoesNotExposeMemberIds()
    {
        await using var host = await CrewsTestHost.StartAsync();
        host.Crews.Seed(FakeCrewRepository.NewcomersCrewId, FakeCrewRepository.UserB);

        var body = await host.Client.GetStringAsync(GroupsUrl(FakeCrewRepository.EventId, FakeCrewRepository.UserA.ToString()));

        body.Should().NotContain(FakeCrewRepository.UserB.ToString()).And.NotContain(FakeCrewRepository.UserA.ToString());
        using var document = JsonDocument.Parse(body);
        foreach (var group in document.RootElement.EnumerateArray())
        {
            group.EnumerateObject().Select(property => property.Name)
                .Should().NotContain(["members", "userId", "memberIds"]);
        }
    }

    [Fact]
    public async Task GetGroups_ForEventWithoutGroups_ReturnsEmptyArray()
    {
        await using var host = await CrewsTestHost.StartAsync();

        var body = await host.Client.GetStringAsync(GroupsUrl(FakeCrewRepository.EmptyEventId));

        using var document = JsonDocument.Parse(body);
        document.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        document.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetGroups_ForUnknownEvent_ReturnsNotFoundProblem()
    {
        await using var host = await CrewsTestHost.StartAsync();

        using var response = await host.Client.GetAsync(GroupsUrl(Guid.NewGuid()));

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api/events/not-a-guid/groups")]
    [InlineData("/api/events/00000000-0000-0000-0000-000000000000/groups")]
    [InlineData("/api/events/11111111-1111-1111-1111-111111111111/groups?userId=nope")]
    [InlineData("/api/events/11111111-1111-1111-1111-111111111111/groups?userId=00000000-0000-0000-0000-000000000000")]
    public async Task GetGroups_WithInvalidIds_ReturnsBadRequestProblem(string url)
    {
        await using var host = await CrewsTestHost.StartAsync();

        using var response = await host.Client.GetAsync(url);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Join_AddsMemberAndReturnsUpdatedGroup()
    {
        await using var host = await CrewsTestHost.StartAsync();

        using var response = await JoinAsync(host, FakeCrewRepository.NewcomersCrewId, FakeCrewRepository.UserA);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("currentMembers").GetInt32().Should().Be(1);
        document.RootElement.GetProperty("joinedByCurrentUser").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Join_Twice_IsIdempotentAndDoesNotDoubleCount()
    {
        await using var host = await CrewsTestHost.StartAsync();
        await JoinAsync(host, FakeCrewRepository.NewcomersCrewId, FakeCrewRepository.UserA);

        using var response = await JoinAsync(host, FakeCrewRepository.NewcomersCrewId, FakeCrewRepository.UserA);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("currentMembers").GetInt32().Should().Be(1);
        host.Crews.CurrentMembers(FakeCrewRepository.NewcomersCrewId).Should().Be(1);
    }

    [Fact]
    public async Task Join_RecordsJoinedAtFromTimeProvider()
    {
        await using var host = await CrewsTestHost.StartAsync();

        await JoinAsync(host, FakeCrewRepository.NewcomersCrewId, FakeCrewRepository.UserA);

        host.Crews.JoinedAt(FakeCrewRepository.NewcomersCrewId, FakeCrewRepository.UserA)
            .Should().Be(CrewsTestHost.FixedNow);
    }

    [Fact]
    public async Task Join_GroupsOfDifferentEvents_IsAllowed()
    {
        await using var host = await CrewsTestHost.StartAsync();
        await JoinAsync(host, FakeCrewRepository.NewcomersCrewId, FakeCrewRepository.UserA);

        using var response = await JoinAsync(host, FakeCrewRepository.TinyCrewId, FakeCrewRepository.UserA);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        host.Crews.CurrentMembers(FakeCrewRepository.NewcomersCrewId).Should().Be(1);
        host.Crews.CurrentMembers(FakeCrewRepository.TinyCrewId).Should().Be(1);
    }

    [Fact]
    public async Task Join_FullGroup_ReturnsConflictProblemWithoutAddingMember()
    {
        await using var host = await CrewsTestHost.StartAsync();
        host.Crews.Seed(FakeCrewRepository.TinyCrewId, FakeCrewRepository.UserA);
        host.Crews.Seed(FakeCrewRepository.TinyCrewId, FakeCrewRepository.UserB);

        using var response = await JoinAsync(host, FakeCrewRepository.TinyCrewId, FakeCrewRepository.UserC);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.Conflict);
        host.Crews.CurrentMembers(FakeCrewRepository.TinyCrewId).Should().Be(2);
    }

    [Fact]
    public async Task Join_SecondGroupOfTheSameEvent_ReturnsConflictProblem()
    {
        await using var host = await CrewsTestHost.StartAsync();
        await JoinAsync(host, FakeCrewRepository.NewcomersCrewId, FakeCrewRepository.UserA);

        using var response = await JoinAsync(host, FakeCrewRepository.CyclistsCrewId, FakeCrewRepository.UserA);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.Conflict);
        host.Crews.CurrentMembers(FakeCrewRepository.CyclistsCrewId).Should().Be(0);
    }

    [Fact]
    public async Task Join_UnknownGroupOrUser_ReturnsNotFoundProblem()
    {
        await using var host = await CrewsTestHost.StartAsync();

        using var unknownGroup = await JoinAsync(host, Guid.NewGuid(), FakeCrewRepository.UserA);
        using var unknownUser = await JoinAsync(host, FakeCrewRepository.NewcomersCrewId, FakeCrewRepository.UnknownUser);

        await ProblemResponseAssertions.AssertAsync(unknownGroup, HttpStatusCode.NotFound);
        await ProblemResponseAssertions.AssertAsync(unknownUser, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"userId\":\"not-a-guid\"}")]
    [InlineData("{\"userId\":\"00000000-0000-0000-0000-000000000000\"}")]
    [InlineData("not json")]
    public async Task Join_WithMissingOrInvalidUserId_ReturnsBadRequestProblem(string json)
    {
        await using var host = await CrewsTestHost.StartAsync();
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        using var response = await host.Client.PostAsync(MembersUrl(FakeCrewRepository.NewcomersCrewId), content);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Join_WithInvalidGroupId_ReturnsBadRequestProblem(string groupId)
    {
        await using var host = await CrewsTestHost.StartAsync();

        using var response = await host.Client.PostAsJsonAsync(
            $"/api/groups/{groupId}/members", new { userId = FakeCrewRepository.UserA });

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Leave_RemovesMemberAndReturnsNoContent()
    {
        await using var host = await CrewsTestHost.StartAsync();
        host.Crews.Seed(FakeCrewRepository.NewcomersCrewId, FakeCrewRepository.UserA);

        using var response = await host.Client.DeleteAsync(
            $"{MembersUrl(FakeCrewRepository.NewcomersCrewId)}/{FakeCrewRepository.UserA}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        host.Crews.CurrentMembers(FakeCrewRepository.NewcomersCrewId).Should().Be(0);
    }

    [Fact]
    public async Task Leave_Twice_IsIdempotent()
    {
        await using var host = await CrewsTestHost.StartAsync();
        host.Crews.Seed(FakeCrewRepository.NewcomersCrewId, FakeCrewRepository.UserA);
        host.Crews.Seed(FakeCrewRepository.NewcomersCrewId, FakeCrewRepository.UserB);
        var url = $"{MembersUrl(FakeCrewRepository.NewcomersCrewId)}/{FakeCrewRepository.UserA}";
        await host.Client.DeleteAsync(url);

        using var second = await host.Client.DeleteAsync(url);

        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        host.Crews.CurrentMembers(FakeCrewRepository.NewcomersCrewId).Should().Be(1);
    }

    // leaveGroup dokumentuje tylko 204 dla poprawnych UUID: nieznana grupa lub uzytkownik sa idempotentnym no-op.
    [Theory]
    [InlineData("99999999-9999-9999-9999-999999999999", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")]
    [InlineData("22222222-2222-2222-2222-222222222222", "99999999-9999-9999-9999-999999999999")]
    public async Task Leave_ForUnknownResource_ReturnsNoContent(string groupId, string userId)
    {
        await using var host = await CrewsTestHost.StartAsync();

        using var response = await host.Client.DeleteAsync($"/api/groups/{groupId}/members/{userId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("/api/groups/not-a-guid/members/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")]
    [InlineData("/api/groups/00000000-0000-0000-0000-000000000000/members/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")]
    [InlineData("/api/groups/22222222-2222-2222-2222-222222222222/members/not-a-guid")]
    [InlineData("/api/groups/22222222-2222-2222-2222-222222222222/members/00000000-0000-0000-0000-000000000000")]
    public async Task Leave_WithInvalidIds_ReturnsBadRequestProblem(string url)
    {
        await using var host = await CrewsTestHost.StartAsync();

        using var response = await host.Client.DeleteAsync(url);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }
}
