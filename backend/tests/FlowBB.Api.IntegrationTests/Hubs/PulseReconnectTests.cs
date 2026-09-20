using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using FlowBB.Api.Endpoints.Events;
using FlowBB.Api.Endpoints.Pulse;
using FlowBB.Api.Hubs;
using FlowBB.Api.IntegrationTests.Infrastructure;
using FlowBB.Application.Abstractions.Realtime;
using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Pulse;
using FlowBB.Domain.Attendance;
using FlowBB.Domain.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowBB.Api.IntegrationTests.Hubs;

/// <summary>
/// Scenariusz dashboardu po utracie polaczenia (issue #50): host z prawdziwymi modulami Attendance i PULSE oraz prawdziwym
/// hubem. Awarie sieci symuluje przelaczalny handler HTTP, wiec testy nie uzywaja <c>Thread.Sleep</c>, a kazde oczekiwanie
/// ma limit czasu. Zasady dla frontendu: docs/REALTIME.md.
/// </summary>
public sealed class PulseReconnectTests
{
    private const string PulseUpdated = "PulseUpdated";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static Guid User(int number) => new($"00000000-0000-0000-0000-{number:D12}");

    private static Channel<JsonElement> Subscribe(HubConnection connection)
    {
        var messages = Channel.CreateUnbounded<JsonElement>();
        connection.On<JsonElement>(PulseUpdated, message => messages.Writer.TryWrite(message));
        return messages;
    }

    private static Task<JsonElement> NextAsync(Channel<JsonElement> messages) =>
        messages.Reader.ReadAsync().AsTask().WaitAsync(Timeout);

    private static PulseUpdate Update(Guid eventId, int participantsCount) => new(
        eventId,
        participantsCount,
        new ModalSplit(PublicTransport: participantsCount, Walking: 0, Bike: 0, Car: 0, Unknown: 0),
        ParticipantsWithoutReturn: 0,
        new DateTimeOffset(2026, 9, 19, 17, 20, 0, TimeSpan.Zero));

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task AfterAnOutage_TheClientReconnectsWithoutReplayAndRecoversTheStateWithGet()
    {
        await using var host = await RealtimeHost.StartAsync();
        await using var connection = host.CreateConnection();
        var messages = Subscribe(connection);
        var reconnecting = Signal();
        var reconnected = Signal();
        connection.Reconnecting += _ => Task.FromResult(reconnecting.TrySetResult());
        connection.Reconnected += _ => Task.FromResult(reconnected.TrySetResult());
        await connection.StartAsync();

        await host.DeclareAsync(User(1));
        (await NextAsync(messages)).GetProperty("participantsCount").GetInt32().Should().Be(1);

        host.Network.GoOffline();
        await reconnecting.Task.WaitAsync(Timeout);
        await host.DeclareAsync(User(2));
        await host.DeclareAsync(User(3));
        var stateDuringOutage = await host.GetParticipantsAsync();
        host.Network.GoOnline();
        await reconnected.Task.WaitAsync(Timeout);

        await host.DeclareAsync(User(4));
        var firstAfterReconnect = await NextAsync(messages);

        stateDuringOutage.Should().Be(3, "GET returns the current state even when the hub is unreachable");
        firstAfterReconnect.GetProperty("participantsCount").GetInt32().Should().Be(
            4, "messages published during the outage are not replayed; the client re-syncs with GET");
        (await host.GetParticipantsAsync()).Should().Be(4);
        connection.State.Should().Be(HubConnectionState.Connected);
    }

    [Fact]
    public async Task AfterAnExplicitRestart_TheClientReceivesTheNextUpdatesInOrder()
    {
        await using var host = await RealtimeHost.StartAsync();
        await using var connection = host.CreateConnection();
        var messages = Subscribe(connection);
        await connection.StartAsync();
        await host.DeclareAsync(User(1));
        await NextAsync(messages);

        await connection.StopAsync();
        await host.DeclareAsync(User(2));
        await connection.StartAsync();
        await host.DeclareAsync(User(3));
        await host.DeclareAsync(User(4));
        var first = await NextAsync(messages);
        var second = await NextAsync(messages);

        first.GetProperty("participantsCount").GetInt32().Should().Be(3);
        second.GetProperty("participantsCount").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task TwoClients_BothReceiveUpdatesOfEveryEventAndTellThemApartByEventId()
    {
        await using var host = await PulseHubTestHost.StartAsync();
        await using var first = host.CreateConnection();
        await using var second = host.CreateConnection();
        var firstMessages = Subscribe(first);
        var secondMessages = Subscribe(second);
        await Task.WhenAll(first.StartAsync(), second.StartAsync());
        var otherEvent = Guid.Parse("33333333-3333-3333-3333-333333333333");

        await host.Notifier.PublishAsync(Update(EventId, 83));
        await host.Notifier.PublishAsync(Update(otherEvent, 47));

        foreach (var messages in new[] { firstMessages, secondMessages })
        {
            var received = new[] { await NextAsync(messages), await NextAsync(messages) };
            received.Select(message => message.GetProperty("eventId").GetGuid()).Should().Equal(EventId, otherEvent);
            received.Select(message => message.GetProperty("participantsCount").GetInt32()).Should().Equal(83, 47);
        }
    }

    /// <summary>Host z prawdziwymi modulami Attendance i PULSE, hubem oraz wspolnym stanem w pamieci.</summary>
    private sealed class RealtimeHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public HttpClient Client { get; }
        public NetworkSwitch Network { get; } = new();

        private RealtimeHost(WebApplication app)
        {
            _app = app;
            Client = app.GetTestClient();
        }

        public static async Task<RealtimeHost> StartAsync()
        {
            var store = new InMemoryPulseStore(EventId);
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddProblemDetails();
            builder.Services.AddAttendanceModule();
            builder.Services.AddPulseModule();
            builder.Services.AddPulseHub();
            builder.Services.AddSingleton<IAttendanceRepository>(store);
            builder.Services.AddSingleton<IPulseDataReader>(store);

            var app = builder.Build();
            app.MapAttendanceEndpoints();
            app.MapPulseEndpoints();
            app.MapPulseHub();
            await app.StartAsync();
            return new RealtimeHost(app);
        }

        public HubConnection CreateConnection() => new HubConnectionBuilder()
            .WithUrl(new Uri(_app.GetTestServer().BaseAddress, "/hubs/pulse"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                // Klient usuwa handler przy zatrzymaniu i przy kazdej probie reconnect, wiec fabryka tworzy nowy za kazdym razem.
                options.HttpMessageHandlerFactory = _ => new SwitchableHandler(_app.GetTestServer().CreateHandler(), Network);
            })
            .WithAutomaticReconnect(new FastRetryPolicy())
            .Build();

        public Task<HttpResponseMessage> DeclareAsync(Guid userId) => Client.PostAsJsonAsync(
            $"/api/events/{EventId}/attendance", new { userId, transportMode = "Walking" });

        public async Task<int> GetParticipantsAsync()
        {
            var pulse = await Client.GetFromJsonAsync<JsonElement>($"/api/pulse/events/{EventId}");
            return pulse.GetProperty("participantsCount").GetInt32();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.DisposeAsync();
        }
    }

    /// <summary>Wspolny przelacznik awarii sieci: zrywa trwajace zapytania i odrzuca nowe, dopoki nie wroci siec.</summary>
    private sealed class NetworkSwitch
    {
        private volatile bool _offline;
        private volatile CancellationTokenSource _outage = new();

        public bool IsOffline => _offline;

        public CancellationToken OutageToken => _outage.Token;

        public void GoOffline()
        {
            _offline = true;
            _outage.Cancel();
        }

        public void GoOnline()
        {
            _outage = new CancellationTokenSource();
            _offline = false;
        }
    }

    /// <summary>Handler HTTP zachowujacy sie jak przy utracie sieci, gdy <see cref="NetworkSwitch"/> jest wylaczony.</summary>
    private sealed class SwitchableHandler(HttpMessageHandler inner, NetworkSwitch network) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (network.IsOffline)
            {
                throw new HttpRequestException("Simulated network outage.");
            }

            var outage = network.OutageToken;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, outage);
            try
            {
                return await base.SendAsync(request, linked.Token);
            }
            catch (OperationCanceledException) when (outage.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new HttpRequestException("Simulated network outage.");
            }
        }
    }

    private sealed class FastRetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext) => TimeSpan.FromMilliseconds(50);
    }

    /// <summary>Stan wydarzenia w pamieci dla Attendance i PULSE: licznik jest wyliczany z deklaracji, jak w adapterze Neo4j.</summary>
    private sealed class InMemoryPulseStore(Guid eventId) : IAttendanceRepository, IPulseDataReader
    {
        private readonly Dictionary<Guid, TransportMode> _attendance = [];

        public Task<AttendanceUpsertPersistenceResult?> UpsertAsync(
            AttendanceIntent attendance, CancellationToken cancellationToken = default)
        {
            if (attendance.EventId != eventId)
            {
                return Task.FromResult<AttendanceUpsertPersistenceResult?>(null);
            }

            var isNew = _attendance.TryAdd(attendance.UserId, attendance.TransportMode);
            _attendance[attendance.UserId] = attendance.TransportMode;
            return Task.FromResult<AttendanceUpsertPersistenceResult?>(
                new AttendanceUpsertPersistenceResult(isNew, _attendance.Count, Split()));
        }

        public Task<AttendanceDeletePersistenceResult> DeleteAsync(
            Guid eventIdToDelete, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AttendanceDeletePersistenceResult(_attendance.Remove(userId), _attendance.Count, Split()));

        public Task<IReadOnlyList<PulsePoint>> GetPointsAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PulsePoint>>(
                id == eventId ? _attendance.Values.Select(mode => new PulsePoint(49.8224, 19.0443, mode)).ToList() : []);

        public Task<PulseEventInfo?> GetEventAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == eventId ? new PulseEventInfo(eventId, "Koncert na Rynku") : null);

        public Task<IReadOnlyList<PulseEventInfo>> GetEventsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PulseEventInfo>>([new PulseEventInfo(eventId, "Koncert na Rynku")]);

        public Task<IReadOnlyList<PulseEventSnapshot>> GetEventsWithPointsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PulseEventSnapshot>>(
            [
                new(
                    new PulseEventInfo(eventId, "Koncert na Rynku"),
                    _attendance.Values.Select(mode => new PulsePoint(49.8224, 19.0443, mode)).ToList())
            ]);

        private ModalSplit Split() => new(
            _attendance.Values.Count(mode => mode == TransportMode.PublicTransport),
            _attendance.Values.Count(mode => mode == TransportMode.Walking),
            _attendance.Values.Count(mode => mode == TransportMode.Bike),
            _attendance.Values.Count(mode => mode == TransportMode.Car),
            _attendance.Values.Count(mode => mode == TransportMode.Unknown));
    }
}
