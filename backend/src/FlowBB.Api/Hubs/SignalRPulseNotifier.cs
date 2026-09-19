using FlowBB.Api.Endpoints.Pulse;
using FlowBB.Application.Abstractions.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace FlowBB.Api.Hubs;

public sealed class SignalRPulseNotifier : IPulseNotifier
{
    private const string PulseUpdated = "PulseUpdated";
    private readonly IHubContext<PulseHub> _hubContext;

    public SignalRPulseNotifier(IHubContext<PulseHub> hubContext)
    {
        ArgumentNullException.ThrowIfNull(hubContext);
        _hubContext = hubContext;
    }

    public Task PublishAsync(PulseUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var message = new PulseUpdatedMessage(
            update.EventId,
            update.ParticipantsCount,
            update.ModalSplit.ToResponse(),
            update.ParticipantsWithoutReturn,
            update.ChangedAt);

        return _hubContext.Clients.All.SendAsync(PulseUpdated, message, cancellationToken);
    }

    private sealed record PulseUpdatedMessage(
        Guid EventId,
        int ParticipantsCount,
        ModalSplitResponse ModalSplit,
        int ParticipantsWithoutReturn,
        DateTimeOffset ChangedAt);
}
