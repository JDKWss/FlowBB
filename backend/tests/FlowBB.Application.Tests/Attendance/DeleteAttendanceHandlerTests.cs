using FluentAssertions;
using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Attendance.DeleteAttendance;
using FlowBB.Domain.Common;
using Moq;

namespace FlowBB.Application.Tests.Attendance;

public sealed class DeleteAttendanceHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 17, 25, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_WhenRepeated_IsIdempotent()
    {
        var command = new DeleteAttendanceCommand(Guid.NewGuid(), Guid.NewGuid());
        var modalSplit = ModalSplit();
        using var cancellation = new CancellationTokenSource();
        var repository = new Mock<IAttendanceRepository>(MockBehavior.Strict);
        repository
            .SetupSequence(x => x.DeleteAsync(command.EventId, command.UserId, cancellation.Token))
            .ReturnsAsync(new AttendanceDeletePersistenceResult(true, 82, modalSplit))
            .ReturnsAsync(new AttendanceDeletePersistenceResult(false, 82, modalSplit));
        var handler = new DeleteAttendanceHandler(repository.Object, new FixedTimeProvider(Now));

        var first = await handler.HandleAsync(command, cancellation.Token);
        var repeated = await handler.HandleAsync(command, cancellation.Token);

        first.WasDeleted.Should().BeTrue();
        repeated.WasDeleted.Should().BeFalse();
        repeated.ParticipantsCount.Should().Be(first.ParticipantsCount);
        repeated.ModalSplit.Should().BeEquivalentTo(first.ModalSplit);
        repeated.ChangedAt.Should().Be(Now);
        repository.VerifyAll();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_WithEmptyIdentifier_ThrowsWithoutCallingRepository(bool emptyEventId)
    {
        var command = new DeleteAttendanceCommand(
            emptyEventId ? Guid.Empty : Guid.NewGuid(),
            emptyEventId ? Guid.NewGuid() : Guid.Empty);
        var repository = new Mock<IAttendanceRepository>(MockBehavior.Strict);
        var handler = new DeleteAttendanceHandler(repository.Object, new FixedTimeProvider(Now));

        var action = () => handler.HandleAsync(command);

        await action.Should().ThrowAsync<ArgumentException>();
        repository.VerifyNoOtherCalls();
    }

    private static IReadOnlyDictionary<TransportMode, int> ModalSplit() =>
        new Dictionary<TransportMode, int>
        {
            [TransportMode.PublicTransport] = 48,
            [TransportMode.Walking] = 18,
            [TransportMode.Bike] = 6,
            [TransportMode.Car] = 7,
            [TransportMode.Unknown] = 3
        };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
