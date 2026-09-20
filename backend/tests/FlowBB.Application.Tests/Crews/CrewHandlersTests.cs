using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Crews;
using FlowBB.Application.Crews.GetEventGroups;
using FlowBB.Application.Crews.JoinCrew;
using FlowBB.Application.Crews.LeaveCrew;
using FlowBB.Domain.Crews;
using FluentAssertions;
using Moq;

namespace FlowBB.Application.Tests.Crews;

public class CrewHandlersTests
{
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CrewId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly Mock<ICrewRepository> _crews = new();
    private readonly Mock<IEventLookup> _events = new();

    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private JoinCrewHandler CreateJoinHandler() => new(_crews.Object, new FixedTimeProvider(Now));

    private static CrewSummary Summary(bool joined = false) => new(
        CrewId, EventId, "Ekipa", "Opis", 3, 6, ["muzyka"], new MeetingPoint("Rynek", 49.82, 19.04), joined);

    [Fact]
    public async Task GetEventGroups_WhenEventMissing_ReturnsNullWithoutReadingCrews()
    {
        _events.Setup(e => e.ExistsAsync(EventId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var handler = new GetEventGroupsHandler(_crews.Object, _events.Object);

        var result = await handler.HandleAsync(EventId, UserId);

        result.Should().BeNull();
        _crews.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetEventGroups_ReturnsGroupsAndPassesUserToRepository()
    {
        var groups = new[] { Summary(joined: true) };
        _events.Setup(e => e.ExistsAsync(EventId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _crews.Setup(c => c.ListByEventAsync(EventId, UserId, It.IsAny<CancellationToken>())).ReturnsAsync(groups);
        var handler = new GetEventGroupsHandler(_crews.Object, _events.Object);

        var result = await handler.HandleAsync(EventId, UserId);

        result.Should().Equal(groups);
    }

    [Fact]
    public async Task GetEventGroups_WithoutUser_PassesNullUser()
    {
        _events.Setup(e => e.ExistsAsync(EventId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _crews.Setup(c => c.ListByEventAsync(EventId, null, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var handler = new GetEventGroupsHandler(_crews.Object, _events.Object);

        var result = await handler.HandleAsync(EventId, null);

        result.Should().BeEmpty();
        _crews.Verify(c => c.ListByEventAsync(EventId, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetEventGroups_WithEmptyIds_Throws()
    {
        var handler = new GetEventGroupsHandler(_crews.Object, _events.Object);

        var emptyEvent = () => handler.HandleAsync(Guid.Empty, null);
        var emptyUser = () => handler.HandleAsync(EventId, Guid.Empty);

        await emptyEvent.Should().ThrowAsync<ArgumentException>();
        await emptyUser.Should().ThrowAsync<ArgumentException>();
        _events.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(JoinCrewOutcome.Joined)]
    [InlineData(JoinCrewOutcome.AlreadyMember)]
    [InlineData(JoinCrewOutcome.Full)]
    [InlineData(JoinCrewOutcome.InAnotherCrew)]
    [InlineData(JoinCrewOutcome.CrewNotFound)]
    [InlineData(JoinCrewOutcome.UserNotFound)]
    public async Task JoinCrew_ReturnsRepositoryResultUnchanged(JoinCrewOutcome outcome)
    {
        var expected = new CrewJoinResult(outcome, outcome <= JoinCrewOutcome.AlreadyMember ? Summary(true) : null);
        _crews.Setup(c => c.TryJoinAsync(CrewId, UserId, Now, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await CreateJoinHandler().HandleAsync(CrewId, UserId);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task JoinCrew_WithEmptyIds_ThrowsAndDoesNotCallRepository(bool emptyCrew)
    {
        var act = () => CreateJoinHandler()
            .HandleAsync(emptyCrew ? Guid.Empty : CrewId, emptyCrew ? UserId : Guid.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
        _crews.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LeaveCrew_CallsRepositoryWithIdsAndToken()
    {
        using var cts = new CancellationTokenSource();
        _crews.Setup(c => c.LeaveAsync(CrewId, UserId, cts.Token)).Returns(Task.CompletedTask);

        await new LeaveCrewHandler(_crews.Object).HandleAsync(CrewId, UserId, cts.Token);

        _crews.Verify(c => c.LeaveAsync(CrewId, UserId, cts.Token), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LeaveCrew_WithEmptyIds_ThrowsAndDoesNotCallRepository(bool emptyCrew)
    {
        var act = () => new LeaveCrewHandler(_crews.Object)
            .HandleAsync(emptyCrew ? Guid.Empty : CrewId, emptyCrew ? UserId : Guid.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
        _crews.VerifyNoOtherCalls();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
