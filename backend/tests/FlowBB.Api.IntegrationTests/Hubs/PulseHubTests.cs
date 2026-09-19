using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;
using FlowBB.Api.Hubs;
using FlowBB.Api.IntegrationTests.Infrastructure;
using FlowBB.Application.Abstractions.Realtime;
using FlowBB.Application.Pulse;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;

namespace FlowBB.Api.IntegrationTests.Hubs;

public sealed class PulseHubTests
{
    private const string EventName = "PulseUpdated";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset ChangedAt = new(2026, 9, 19, 17, 20, 0, TimeSpan.Zero);

    [Fact]
    public async Task PublishAsync_SendsContractPayloadToConnectedClient()
    {
        await using var host = await PulseHubTestHost.StartAsync();
        await using var connection = host.CreateConnection();
        using var pending = CaptureNext(connection);
        await connection.StartAsync();

        await host.Notifier.PublishAsync(CreateUpdate(83));
        var payload = await pending.Message.WaitAsync(Timeout);

        payload.GetProperty("eventId").GetGuid().Should().Be(EventId);
        payload.GetProperty("participantsCount").GetInt32().Should().Be(83);
        payload.GetProperty("participantsWithoutReturn").GetInt32().Should().Be(21);
        payload.GetProperty("changedAt").GetDateTimeOffset().Should().Be(ChangedAt);

        var modalSplit = payload.GetProperty("modalSplit");
        modalSplit.GetProperty("publicTransport").GetInt32().Should().Be(49);
        modalSplit.GetProperty("walking").GetInt32().Should().Be(18);
        modalSplit.GetProperty("bike").GetInt32().Should().Be(6);
        modalSplit.GetProperty("car").GetInt32().Should().Be(7);
        modalSplit.GetProperty("unknown").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task PublishAsync_BroadcastsSameMessageToTwoClients()
    {
        await using var host = await PulseHubTestHost.StartAsync();
        await using var firstConnection = host.CreateConnection();
        await using var secondConnection = host.CreateConnection();
        using var firstPending = CaptureNext(firstConnection);
        using var secondPending = CaptureNext(secondConnection);
        await Task.WhenAll(firstConnection.StartAsync(), secondConnection.StartAsync());

        await host.Notifier.PublishAsync(CreateUpdate(83));
        var messages = await Task.WhenAll(
            firstPending.Message.WaitAsync(Timeout),
            secondPending.Message.WaitAsync(Timeout));

        messages[0].GetRawText().Should().Be(messages[1].GetRawText());
    }

    [Fact]
    public async Task PublishAsync_SendsOnlyFieldsDefinedByContract()
    {
        await using var host = await PulseHubTestHost.StartAsync();
        await using var connection = host.CreateConnection();
        using var pending = CaptureNext(connection);
        await connection.StartAsync();

        await host.Notifier.PublishAsync(CreateUpdate(83));
        var payload = await pending.Message.WaitAsync(Timeout);

        payload.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "eventId", "participantsCount", "modalSplit", "participantsWithoutReturn", "changedAt");
        payload.GetProperty("modalSplit").EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "publicTransport", "walking", "bike", "car", "unknown");
    }

    [Fact]
    public async Task PublishAsync_WithoutConnectedClients_DoesNotThrow()
    {
        await using var host = await PulseHubTestHost.StartAsync();

        var action = () => host.Notifier.PublishAsync(CreateUpdate(83));

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public void PulseHub_DeclaresNoPublicMethods()
    {
        var methods = typeof(PulseHub).GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        methods.Should().BeEmpty();
    }

    [Fact]
    public async Task PublishAsync_DeliversParticipantCountsInOrder()
    {
        await using var host = await PulseHubTestHost.StartAsync();
        await using var connection = host.CreateConnection();
        var messages = Channel.CreateUnbounded<JsonElement>();
        using var subscription = connection.On<JsonElement>(EventName, message =>
        {
            messages.Writer.TryWrite(message);
        });
        await connection.StartAsync();

        await host.Notifier.PublishAsync(CreateUpdate(82));
        await host.Notifier.PublishAsync(CreateUpdate(83));
        var first = await messages.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
        var second = await messages.Reader.ReadAsync().AsTask().WaitAsync(Timeout);

        first.GetProperty("participantsCount").GetInt32().Should().Be(82);
        second.GetProperty("participantsCount").GetInt32().Should().Be(83);
    }

    private static PulseUpdate CreateUpdate(int participantsCount) => new(
        EventId,
        participantsCount,
        new ModalSplit(PublicTransport: 49, Walking: 18, Bike: 6, Car: 7, Unknown: 3),
        ParticipantsWithoutReturn: 21,
        ChangedAt);

    private static PendingMessage CaptureNext(HubConnection connection)
    {
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscription = connection.On<JsonElement>(EventName, message => completion.TrySetResult(message));
        return new PendingMessage(completion.Task, subscription);
    }

    private sealed record PendingMessage(Task<JsonElement> Message, IDisposable Subscription) : IDisposable
    {
        public void Dispose() => Subscription.Dispose();
    }
}
