using FlowBB.Application.Abstractions.Realtime;

namespace FlowBB.Api.IntegrationTests.Infrastructure;

/// <summary>Zapamietuje publikowane aktualizacje PULSE (zamiast prawdziwego SignalR).</summary>
public sealed class RecordingPulseNotifier : IPulseNotifier
{
    public List<PulseUpdate> Updates { get; } = [];

    public Task PublishAsync(PulseUpdate update, CancellationToken cancellationToken = default)
    {
        Updates.Add(update);
        return Task.CompletedTask;
    }
}
