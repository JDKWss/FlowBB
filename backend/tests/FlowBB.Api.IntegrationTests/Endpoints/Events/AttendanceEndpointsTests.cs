using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FlowBB.Api.IntegrationTests.Endpoints;
using FlowBB.Api.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace FlowBB.Api.IntegrationTests.Endpoints.Events;

public class AttendanceEndpointsTests
{
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private sealed record Setup(
        AttendanceTestHost Host, FakeAttendanceRepository Repository, RecordingPulseNotifier Notifier) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Host.DisposeAsync();
    }

    private static async Task<Setup> StartAsync()
    {
        var repository = new FakeAttendanceRepository().AddEvent(EventId).AddUser(UserId).AddUser(OtherUserId);
        var notifier = new RecordingPulseNotifier();
        return new Setup(await AttendanceTestHost.StartAsync(repository, notifier), repository, notifier);
    }

    private static string AttendanceUrl(Guid eventId) => $"/api/events/{eventId}/attendance";

    private static Task<HttpResponseMessage> PostAsync(Setup setup, Guid userId, string transportMode) =>
        setup.Host.Client.PostAsJsonAsync(AttendanceUrl(EventId), new { userId, transportMode });

    private static Task<HttpResponseMessage> PostRawAsync(Setup setup, string url, string body) =>
        setup.Host.Client.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task FirstPost_CreatesAttendanceAndMatchesContract()
    {
        await using var setup = await StartAsync();

        var response = await PostAsync(setup, UserId, "PublicTransport");
        var json = await JsonAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.GetProperty("eventId").GetGuid().Should().Be(EventId);
        json.GetProperty("userId").GetGuid().Should().Be(UserId);
        json.GetProperty("transportMode").GetString().Should().Be("PublicTransport");
        json.GetProperty("participantsCount").GetInt32().Should().Be(1);
        json.GetProperty("isNew").GetBoolean().Should().BeTrue();
        json.TryGetProperty("updatedAt", out _).Should().BeTrue();
        json.EnumerateObject().Should().HaveCount(6);
    }

    [Fact]
    public async Task IdenticalRepeatedPost_DoesNotIncreaseParticipantsCount()
    {
        await using var setup = await StartAsync();

        await PostAsync(setup, UserId, "Walking");
        var repeated = await JsonAsync(await PostAsync(setup, UserId, "Walking"));

        repeated.GetProperty("isNew").GetBoolean().Should().BeFalse();
        repeated.GetProperty("participantsCount").GetInt32().Should().Be(1);
        setup.Repository.Count(EventId).Should().Be(1);
    }

    [Fact]
    public async Task ChangingTransportMode_ChangesModalSplitButNotParticipantsCount()
    {
        await using var setup = await StartAsync();

        await PostAsync(setup, UserId, "Bike");
        var updated = await JsonAsync(await PostAsync(setup, UserId, "Car"));

        updated.GetProperty("isNew").GetBoolean().Should().BeFalse();
        updated.GetProperty("participantsCount").GetInt32().Should().Be(1);
        var last = setup.Notifier.Updates.Last();
        last.ParticipantsCount.Should().Be(1);
        last.ModalSplit.Bike.Should().Be(0);
        last.ModalSplit.Car.Should().Be(1);
    }

    [Fact]
    public async Task SecondUser_IncreasesParticipantsCount()
    {
        await using var setup = await StartAsync();

        await PostAsync(setup, UserId, "Walking");
        var second = await JsonAsync(await PostAsync(setup, OtherUserId, "Walking"));

        second.GetProperty("participantsCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task SuccessfulPost_PublishesAggregateOnlyAfterSave()
    {
        await using var setup = await StartAsync();

        await PostAsync(setup, UserId, "PublicTransport");

        var update = setup.Notifier.Updates.Should().ContainSingle().Subject;
        update.EventId.Should().Be(EventId);
        update.ParticipantsCount.Should().Be(1);
        update.ModalSplit.PublicTransport.Should().Be(1);
        update.ParticipantsWithoutReturn.Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Post_ForUnknownEventOrUser_Returns404WithoutPublishing(bool unknownEvent)
    {
        await using var setup = await StartAsync();
        var eventId = unknownEvent ? Guid.NewGuid() : EventId;
        var userId = unknownEvent ? UserId : Guid.NewGuid();

        var response = await setup.Host.Client.PostAsJsonAsync(
            AttendanceUrl(eventId), new { userId, transportMode = "Walking" });

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.NotFound);
        setup.Notifier.Updates.Should().BeEmpty();
    }

    [Theory]
    [InlineData("{\"userId\":\"00000000-0000-0000-0000-000000000000\",\"transportMode\":\"Walking\"}")]
    [InlineData("{\"userId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"transportMode\":\"Teleport\"}")]
    [InlineData("{\"userId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"transportMode\":99}")]
    [InlineData("{\"userId\":\"not-a-guid\",\"transportMode\":\"Walking\"}")]
    [InlineData("{\"transportMode\":\"Walking\"}")]
    [InlineData("{}")]
    [InlineData("")]
    [InlineData("not json")]
    public async Task Post_WithInvalidBody_Returns400WithoutPublishing(string body)
    {
        await using var setup = await StartAsync();

        var response = await PostRawAsync(setup, AttendanceUrl(EventId), body);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
        setup.Notifier.Updates.Should().BeEmpty();
        setup.Repository.Count(EventId).Should().Be(0);
    }

    [Theory]
    [InlineData("/api/events/00000000-0000-0000-0000-000000000000/attendance")]
    [InlineData("/api/events/not-a-guid/attendance")]
    public async Task Post_WithInvalidEventId_Returns400(string url)
    {
        await using var setup = await StartAsync();

        var response = await PostRawAsync(setup, url, "{\"userId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"transportMode\":\"Walking\"}");

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
        setup.Notifier.Updates.Should().BeEmpty();
    }

    [Fact]
    public async Task Post_WhenPersistenceFails_DoesNotPublish()
    {
        await using var setup = await StartAsync();
        setup.Repository.FailWith = new InvalidOperationException("db down");

        var act = () => PostAsync(setup, UserId, "Walking");

        await act.Should().ThrowAsync<InvalidOperationException>();
        setup.Notifier.Updates.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_ExistingAttendance_Returns204AndPublishesNewAggregate()
    {
        await using var setup = await StartAsync();
        await PostAsync(setup, UserId, "Walking");
        await PostAsync(setup, OtherUserId, "Car");
        setup.Notifier.Updates.Clear();

        var response = await setup.Host.Client.DeleteAsync($"{AttendanceUrl(EventId)}/{UserId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var update = setup.Notifier.Updates.Should().ContainSingle().Subject;
        update.ParticipantsCount.Should().Be(1);
        update.ModalSplit.Walking.Should().Be(0);
        update.ModalSplit.Car.Should().Be(1);
    }

    [Fact]
    public async Task Delete_Twice_IsSafeAndSecondCallPublishesNothing()
    {
        await using var setup = await StartAsync();
        await PostAsync(setup, UserId, "Walking");
        var url = $"{AttendanceUrl(EventId)}/{UserId}";

        var first = await setup.Host.Client.DeleteAsync(url);
        var publishedAfterFirst = setup.Notifier.Updates.Count;
        var second = await setup.Host.Client.DeleteAsync(url);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        setup.Notifier.Updates.Should().HaveCount(publishedAfterFirst);
        setup.Repository.Count(EventId).Should().Be(0);
    }

    // deleteAttendance dokumentuje tylko 204 dla poprawnych UUID: nieznane wydarzenie, uzytkownik lub deklaracja sa no-op.
    [Theory]
    [InlineData("99999999-9999-9999-9999-999999999999", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")]
    [InlineData("11111111-1111-1111-1111-111111111111", "99999999-9999-9999-9999-999999999999")]
    public async Task Delete_ForUnknownResource_Returns204WithoutPublishing(string eventId, string userId)
    {
        await using var setup = await StartAsync();

        var response = await setup.Host.Client.DeleteAsync($"/api/events/{eventId}/attendance/{userId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        setup.Notifier.Updates.Should().BeEmpty();
    }

    [Theory]
    [InlineData("/api/events/00000000-0000-0000-0000-000000000000/attendance/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")]
    [InlineData("/api/events/not-a-guid/attendance/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")]
    [InlineData("/api/events/11111111-1111-1111-1111-111111111111/attendance/00000000-0000-0000-0000-000000000000")]
    [InlineData("/api/events/11111111-1111-1111-1111-111111111111/attendance/not-a-guid")]
    public async Task Delete_WithInvalidIds_Returns400(string url)
    {
        await using var setup = await StartAsync();

        var response = await setup.Host.Client.DeleteAsync(url);

        await ProblemResponseAssertions.AssertAsync(response, HttpStatusCode.BadRequest);
        setup.Notifier.Updates.Should().BeEmpty();
    }
}
