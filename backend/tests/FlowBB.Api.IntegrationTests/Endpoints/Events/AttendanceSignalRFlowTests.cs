using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using FlowBB.Api.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;

namespace FlowBB.Api.IntegrationTests.Endpoints.Events;

/// <summary>
/// Scenariusz "Ide -> licznik +1": prawdziwy endpoint Attendance publikuje przez prawdziwy hub SignalR,
/// a klient dashboardu odbiera PulseUpdated. Dopelnia kryteria issue #19.
/// </summary>
public sealed class AttendanceSignalRFlowTests
{
    private const string PulseUpdated = "PulseUpdated";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan NoMessageWindow = TimeSpan.FromMilliseconds(400);
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private sealed class Flow : IAsyncDisposable
    {
        private readonly HubConnection _connection;
        private readonly Channel<JsonElement> _messages = Channel.CreateUnbounded<JsonElement>();

        public AttendanceFlowTestHost Host { get; }
        public FakeAttendanceRepository Repository { get; }

        private Flow(AttendanceFlowTestHost host, FakeAttendanceRepository repository, HubConnection connection)
        {
            Host = host;
            Repository = repository;
            _connection = connection;
        }

        public static async Task<Flow> StartAsync(int existingAttendees = 0, DateTimeOffset? eventEnd = null)
        {
            var repository = new FakeAttendanceRepository().AddEvent(EventId);
            var pulseReader = new FakePulseDataReader().AddEvent(EventId, "Wydarzenie", [], eventEnd);
            var host = await AttendanceFlowTestHost.StartAsync(repository, pulseReader);
            var connection = host.CreateConnection();
            var flow = new Flow(host, repository, connection);

            foreach (var userId in Enumerable.Range(1, existingAttendees).Select(UserGuid))
            {
                repository.AddUser(userId);
                await flow.PostAsync(userId, "Walking");
            }

            connection.On<JsonElement>(PulseUpdated, message => flow._messages.Writer.TryWrite(message));
            await connection.StartAsync();
            return flow;
        }

        public Task<HttpResponseMessage> PostAsync(Guid userId, string transportMode) =>
            Host.Client.PostAsJsonAsync($"/api/events/{EventId}/attendance", new { userId, transportMode });

        public Task<HttpResponseMessage> DeleteAsync(Guid userId) =>
            Host.Client.DeleteAsync($"/api/events/{EventId}/attendance/{userId}");

        public Task<JsonElement> NextMessageAsync() => _messages.Reader.ReadAsync().AsTask().WaitAsync(Timeout);

        public async Task<bool> ReceivesNothingAsync()
        {
            try
            {
                await _messages.Reader.ReadAsync().AsTask().WaitAsync(NoMessageWindow);
                return false;
            }
            catch (TimeoutException)
            {
                return true;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
            await Host.DisposeAsync();
        }
    }

    private static Guid UserGuid(int number) => new($"00000000-0000-0000-0000-{number:D12}");

    [Fact]
    public async Task ClickingGo_MovesCounterFrom82To83OnTheDashboard()
    {
        await using var flow = await Flow.StartAsync(existingAttendees: 82);
        var newcomer = UserGuid(1000);
        flow.Repository.AddUser(newcomer);

        var response = await flow.PostAsync(newcomer, "PublicTransport");
        var message = await flow.NextMessageAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        message.GetProperty("eventId").GetGuid().Should().Be(EventId);
        message.GetProperty("participantsCount").GetInt32().Should().Be(83);
        message.GetProperty("modalSplit").GetProperty("walking").GetInt32().Should().Be(82);
        message.GetProperty("modalSplit").GetProperty("publicTransport").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task PublicTransportOnLateEvent_PublishesParticipantsWithoutReturn()
    {
        var lateEnd = new DateTimeOffset(2026, 9, 25, 21, 0, 0, TimeSpan.Zero); // 23:00 w Warszawie
        await using var flow = await Flow.StartAsync(existingAttendees: 2, eventEnd: lateEnd);
        var user = UserGuid(1000);
        flow.Repository.AddUser(user);

        await flow.PostAsync(user, "PublicTransport");
        var afterPost = await flow.NextMessageAsync();
        await flow.DeleteAsync(user);
        var afterDelete = await flow.NextMessageAsync();

        afterPost.GetProperty("participantsWithoutReturn").GetInt32().Should().Be(1);
        afterDelete.GetProperty("participantsWithoutReturn").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task PublicTransportOnEarlyEvent_PublishesNoReturnGap()
    {
        var earlyEnd = new DateTimeOffset(2026, 9, 25, 19, 30, 0, TimeSpan.Zero); // 21:30 w Warszawie
        await using var flow = await Flow.StartAsync(eventEnd: earlyEnd);
        var user = UserGuid(1000);
        flow.Repository.AddUser(user);

        await flow.PostAsync(user, "PublicTransport");
        var message = await flow.NextMessageAsync();

        message.GetProperty("participantsWithoutReturn").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task RepeatedIdenticalPost_DoesNotIncreaseTheCounterSeenByTheClient()
    {
        await using var flow = await Flow.StartAsync(existingAttendees: 2);
        var user = UserGuid(1000);
        flow.Repository.AddUser(user);

        await flow.PostAsync(user, "Bike");
        var first = await flow.NextMessageAsync();
        await flow.PostAsync(user, "Bike");
        var repeated = await flow.NextMessageAsync();

        first.GetProperty("participantsCount").GetInt32().Should().Be(3);
        repeated.GetProperty("participantsCount").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task ChangingTransportMode_UpdatesModalSplitWithoutChangingTheCounter()
    {
        await using var flow = await Flow.StartAsync();
        var user = UserGuid(1000);
        flow.Repository.AddUser(user);

        await flow.PostAsync(user, "Bike");
        await flow.NextMessageAsync();
        await flow.PostAsync(user, "Car");
        var changed = await flow.NextMessageAsync();

        changed.GetProperty("participantsCount").GetInt32().Should().Be(1);
        changed.GetProperty("modalSplit").GetProperty("bike").GetInt32().Should().Be(0);
        changed.GetProperty("modalSplit").GetProperty("car").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Delete_PublishesLowerCounterAndRepeatedDeletePublishesNothing()
    {
        await using var flow = await Flow.StartAsync(existingAttendees: 3);

        var first = await flow.DeleteAsync(UserGuid(1));
        var afterDelete = await flow.NextMessageAsync();
        var second = await flow.DeleteAsync(UserGuid(1));

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        afterDelete.GetProperty("participantsCount").GetInt32().Should().Be(2);
        (await flow.ReceivesNothingAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task FailedPost_PublishesNothing()
    {
        await using var flow = await Flow.StartAsync();

        var unknownUser = await flow.PostAsync(UserGuid(999), "Walking");
        var invalidMode = await flow.Host.Client.PostAsJsonAsync(
            $"/api/events/{EventId}/attendance", new { userId = UserGuid(1), transportMode = "Teleport" });

        unknownUser.StatusCode.Should().Be(HttpStatusCode.NotFound);
        invalidMode.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await flow.ReceivesNothingAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task PublishedMessage_ContainsOnlyFieldsDefinedByContract()
    {
        await using var first = await Flow.StartAsync();
        var user = UserGuid(1000);
        first.Repository.AddUser(user);

        await first.PostAsync(user, "Walking");
        var message = await first.NextMessageAsync();

        message.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "eventId", "participantsCount", "modalSplit", "participantsWithoutReturn", "changedAt");
    }
}
