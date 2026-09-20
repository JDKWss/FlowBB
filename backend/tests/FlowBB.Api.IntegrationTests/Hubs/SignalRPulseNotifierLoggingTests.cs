using FlowBB.Api.Hubs;
using FlowBB.Api.IntegrationTests.Infrastructure;
using FlowBB.Application.Abstractions.Realtime;
using FlowBB.Application.Pulse;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace FlowBB.Api.IntegrationTests.Hubs;

public sealed class SignalRPulseNotifierLoggingTests
{
    private static readonly Guid EventGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly PulseUpdate Update = new(EventGuid, 83, new ModalSplit(49, 18, 6, 7, 3), 0, DateTimeOffset.UnixEpoch);

    private sealed class FakeProxy(Exception? failure) : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) =>
            failure is null ? Task.CompletedTask : Task.FromException(failure);
    }

    private sealed class FakeClients(IClientProxy proxy) : IHubClients
    {
        public IClientProxy All => proxy;

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => proxy;

        public IClientProxy Client(string connectionId) => proxy;

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => proxy;

        public IClientProxy Group(string groupName) => proxy;

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => proxy;

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => proxy;

        public IClientProxy User(string userId) => proxy;

        public IClientProxy Users(IReadOnlyList<string> userIds) => proxy;
    }

    private sealed class FakeGroups : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeHubContext(IClientProxy proxy) : IHubContext<PulseHub>
    {
        public IHubClients Clients { get; } = new FakeClients(proxy);

        public IGroupManager Groups { get; } = new FakeGroups();
    }

    private static (SignalRPulseNotifier Notifier, ListLogger<SignalRPulseNotifier> Logger) Create(Exception? failure)
    {
        var logger = new ListLogger<SignalRPulseNotifier>();
        return (new SignalRPulseNotifier(new FakeHubContext(new FakeProxy(failure)), logger), logger);
    }

    [Fact]
    public async Task PublishFailure_IsLoggedAsWarningWithEventIdAndDoesNotFailTheRequest()
    {
        var failure = new InvalidOperationException("transport is down");
        var (notifier, logger) = Create(failure);

        var act = () => notifier.PublishAsync(Update);

        await act.Should().NotThrowAsync();
        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Exception.Should().BeSameAs(failure);
        entry.Message.Should().Contain(EventGuid.ToString()).And.Contain("PulseUpdated");
    }

    [Fact]
    public async Task PublishFailure_LogDoesNotContainAggregatesOrCoordinates()
    {
        var (notifier, logger) = Create(new InvalidOperationException("boom"));

        await notifier.PublishAsync(Update);

        logger.Entries.Single().Message.Should().NotContain("83").And.NotContain("49");
    }

    [Fact]
    public async Task SuccessfulPublish_LogsNothing()
    {
        var (notifier, logger) = Create(failure: null);

        await notifier.PublishAsync(Update);

        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancellation_IsNotSwallowed()
    {
        var (notifier, logger) = Create(new OperationCanceledException());

        var act = () => notifier.PublishAsync(Update);

        await act.Should().ThrowAsync<OperationCanceledException>();
        logger.Entries.Should().BeEmpty();
    }
}
