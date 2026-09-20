using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;

namespace FlowBB.Api.IntegrationTests.Smoke;

/// <summary>Sprawdza na prawdziwym stosie krok 5 scenariusza demo: klient huba dostaje <c>PulseUpdated</c> po zmianie Attendance.</summary>
[Collection(SmokeCollection.Name)]
public sealed class PulseUpdatedSmokeTests : SmokeTestBase
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static HubConnection CreateHub(TaskCompletionSource<JsonElement> received)
    {
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(SmokeEnvironment.BaseUrl!), "/hubs/pulse"))
            .Build();
        hub.On<JsonElement>("PulseUpdated", message =>
        {
            if (message.GetProperty("eventId").GetGuid() == SmokeSeed.Run)
            {
                received.TrySetResult(message);
            }
        });
        return hub;
    }

    private static async Task<JsonElement> WaitAsync(TaskCompletionSource<JsonElement> received)
    {
        var finished = await Task.WhenAny(received.Task, Task.Delay(Timeout));
        finished.Should().BeSameAs(received.Task, "PulseUpdated should arrive within {0}", Timeout);
        return await received.Task;
    }

    [SmokeFact]
    public async Task Declare_PublishesPulseUpdatedWithTheNewCount()
    {
        var before = await Api.ParticipantsAsync(SmokeSeed.Run);
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var hub = CreateHub(received);
        await hub.StartAsync();

        (await Api.DeclareAsync(SmokeSeed.Run, SmokeSeed.FreeUser, "Bike")).Dispose();
        var message = await WaitAsync(received);

        message.GetProperty("participantsCount").GetInt32().Should().Be(before + 1);
        message.GetProperty("modalSplit").GetProperty("bike").GetInt32().Should().BeGreaterThan(0);
        message.TryGetProperty("changedAt", out _).Should().BeTrue();
        message.GetRawText().Should().NotContainEquivalentOf("userId", "PULSE messages carry only aggregates");
    }

    [SmokeFact]
    public async Task Declare_PublishesTheReturnGapInPulseUpdated()
    {
        var before = await Api.ParticipantsWithoutReturnAsync(SmokeSeed.Run);
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var hub = CreateHub(received);
        await hub.StartAsync();

        (await Api.DeclareAsync(SmokeSeed.Run, SmokeSeed.FreeUser, "PublicTransport")).Dispose();
        var message = await WaitAsync(received);

        message.GetProperty("participantsWithoutReturn").GetInt32().Should().Be(before + 1);
        (await Api.ParticipantsWithoutReturnAsync(SmokeSeed.Run)).Should().Be(before + 1, "the message must agree with GET");
    }

    [SmokeFact]
    public async Task Withdraw_PublishesPulseUpdatedWithTheRestoredCount()
    {
        var before = await Api.ParticipantsAsync(SmokeSeed.Run);
        (await Api.DeclareAsync(SmokeSeed.Run, SmokeSeed.FreeUser, "Walking")).Dispose();
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var hub = CreateHub(received);
        await hub.StartAsync();

        (await Api.WithdrawAsync(SmokeSeed.Run, SmokeSeed.FreeUser)).Dispose();
        var message = await WaitAsync(received);

        message.GetProperty("participantsCount").GetInt32().Should().Be(before);
    }
}
