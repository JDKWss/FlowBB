using System.Net;
using FlowBB.Api.IntegrationTests.Endpoints;
using FluentAssertions;

namespace FlowBB.Api.IntegrationTests.Smoke;

[Collection(SmokeCollection.Name)]
public sealed class AttendanceSmokeTests : SmokeTestBase
{
    private static readonly Guid EventId = SmokeSeed.Run;
    private static readonly Guid UserId = SmokeSeed.FreeUser;

    [SmokeFact]
    public async Task Declare_CreatesTheIntentAndRepeatingItDoesNotCountTwice()
    {
        var before = await Api.ParticipantsAsync(EventId);

        using var first = await Api.DeclareAsync(EventId, UserId, "PublicTransport");
        var created = await SmokeClient.ReadAsync(first);
        using var second = await Api.DeclareAsync(EventId, UserId, "PublicTransport");
        var repeated = await SmokeClient.ReadAsync(second);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        created.GetProperty("isNew").GetBoolean().Should().BeTrue();
        created.GetProperty("participantsCount").GetInt32().Should().Be(before + 1);
        repeated.GetProperty("isNew").GetBoolean().Should().BeFalse();
        repeated.GetProperty("participantsCount").GetInt32().Should().Be(before + 1);
        (await Api.ParticipantsAsync(EventId)).Should().Be(before + 1);
    }

    [SmokeFact]
    public async Task Declare_ChangingTheModeMovesTheModalSplitButKeepsTheCount()
    {
        var before = await Api.PulseAsync(EventId);
        var bikeBefore = before.GetProperty("modalSplit").GetProperty("bike").GetInt32();
        (await Api.DeclareAsync(EventId, UserId, "PublicTransport")).Dispose();

        using var changed = await Api.DeclareAsync(EventId, UserId, "Bike");

        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await Api.PulseAsync(EventId);
        after.GetProperty("participantsCount").GetInt32().Should().Be(before.GetProperty("participantsCount").GetInt32() + 1);
        after.GetProperty("modalSplit").GetProperty("bike").GetInt32().Should().Be(bikeBefore + 1);
    }

    [SmokeTheory]
    [InlineData("{\"userId\":\"d1000000-0000-0000-0000-000000000082\",\"transportMode\":\"Teleport\"}", "application/json")]
    [InlineData("{\"userId\":\"00000000-0000-0000-0000-000000000000\",\"transportMode\":\"Walking\"}", "application/json")]
    [InlineData("{}", "application/json")]
    [InlineData("", "application/json")]
    [InlineData("not json", "application/json")]
    [InlineData("{}", "text/plain")]
    [InlineData("{}", null)]
    public async Task Declare_WithInvalidRequest_Returns400AndChangesNothing(string body, string? contentType)
    {
        var before = await Api.ParticipantsAsync(EventId);

        using var response = await Api.PostRawAsync($"/api/events/{EventId}/attendance", body, contentType);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
        (await Api.ParticipantsAsync(EventId)).Should().Be(before);
    }

    [SmokeFact]
    public async Task Declare_WithInvalidEventId_Returns400()
    {
        using var response = await Api.PostJsonAsync(
            "/api/events/not-a-guid/attendance", new { userId = UserId, transportMode = "Walking" });

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }

    [SmokeFact]
    public async Task Declare_WithUnknownEvent_Returns404()
    {
        using var response = await Api.DeclareAsync(SmokeSeed.Unknown, UserId, "Walking");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.NotFound);
    }

    [SmokeFact]
    public async Task Declare_WithUnknownUser_Returns404()
    {
        using var response = await Api.DeclareAsync(EventId, SmokeSeed.Unknown, "Walking");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.NotFound);
    }

    [SmokeFact]
    public async Task Withdraw_IsIdempotentAndRestoresTheCount()
    {
        var before = await Api.ParticipantsAsync(EventId);
        (await Api.DeclareAsync(EventId, UserId, "Walking")).Dispose();

        using var first = await Api.WithdrawAsync(EventId, UserId);
        using var second = await Api.WithdrawAsync(EventId, UserId);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Api.ParticipantsAsync(EventId)).Should().Be(before);
    }

    [SmokeTheory]
    [InlineData("not-a-guid", "d1000000-0000-0000-0000-000000000082")]
    [InlineData("33333333-3333-3333-3333-333333333333", "not-a-guid")]
    public async Task Withdraw_WithInvalidIds_Returns400(string eventId, string userId)
    {
        using var response = await Api.DeleteAsync($"/api/events/{eventId}/attendance/{userId}");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
    }
}
