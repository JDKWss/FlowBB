using FlowBB.Api.Endpoints.Pulse;
using FlowBB.Application.Abstractions.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace FlowBB.Api.Hubs;

/// <summary>
/// Publikuje <c>PulseUpdated</c> do klientow huba. Wywolywany dopiero po zatwierdzeniu zapisu Attendance, dlatego
/// niepowodzenie publikacji jest tylko logowane i nie zamienia udanego, idempotentnego zapisu w blad zadania.
/// Klient odzyskuje stan przez <c>GET /api/pulse/events/{id}</c>. Anulowanie nie jest polykane.
/// </summary>
public sealed class SignalRPulseNotifier : IPulseNotifier
{
    private const string PulseUpdated = "PulseUpdated";
    private readonly IHubContext<PulseHub> _hubContext;
    private readonly ILogger<SignalRPulseNotifier> _logger;

    public SignalRPulseNotifier(IHubContext<PulseHub> hubContext, ILogger<SignalRPulseNotifier> logger)
    {
        ArgumentNullException.ThrowIfNull(hubContext);
        ArgumentNullException.ThrowIfNull(logger);
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task PublishAsync(PulseUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var message = new PulseUpdatedMessage(
            update.EventId,
            update.ParticipantsCount,
            update.ModalSplit.ToResponse(),
            update.ParticipantsWithoutReturn,
            update.ChangedAt);

        try
        {
            await _hubContext.Clients.All.SendAsync(PulseUpdated, message, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Failed to publish {SignalREvent} for event {FlowEventId}. The change is already saved; clients can re-sync with GET /api/pulse/events/{{id}}.",
                PulseUpdated,
                update.EventId);
        }
    }

    private sealed record PulseUpdatedMessage(
        Guid EventId,
        int ParticipantsCount,
        ModalSplitResponse ModalSplit,
        int ParticipantsWithoutReturn,
        DateTimeOffset ChangedAt);
}
