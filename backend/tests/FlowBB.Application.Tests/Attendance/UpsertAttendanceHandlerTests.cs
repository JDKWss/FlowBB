using FluentAssertions;
using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Attendance.UpsertAttendance;
using FlowBB.Domain.Common;
using Moq;
using AttendanceIntent = FlowBB.Domain.Attendance.Attendance;

namespace FlowBB.Application.Tests.Attendance;

public sealed class UpsertAttendanceHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 17, 20, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_CreatesAttendanceAndReturnsTransactionalSnapshot()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        AttendanceIntent? savedAttendance = null;
        var modalSplit = ModalSplit(publicTransport: 49, walking: 18);
        var repository = new Mock<IAttendanceRepository>(MockBehavior.Strict);
        repository
            .Setup(x => x.UpsertAsync(It.IsAny<AttendanceIntent>(), cancellation.Token))
            .Callback<AttendanceIntent, CancellationToken>((attendance, _) => savedAttendance = attendance)
            .ReturnsAsync(new AttendanceUpsertPersistenceResult(true, 83, modalSplit));
        var handler = CreateHandler(repository.Object);

        var result = await handler.HandleAsync(
            new UpsertAttendanceCommand(eventId, userId, TransportMode.PublicTransport),
            cancellation.Token);

        savedAttendance.Should().NotBeNull();
        savedAttendance!.EventId.Should().Be(eventId);
        savedAttendance.UserId.Should().Be(userId);
        savedAttendance.TransportMode.Should().Be(TransportMode.PublicTransport);
        savedAttendance.UpdatedAt.Should().Be(Now);
        result.Should().Be(new UpsertAttendanceResult(
            eventId,
            userId,
            TransportMode.PublicTransport,
            83,
            true,
            Now,
            modalSplit));
        repository.VerifyAll();
    }

    [Fact]
    public async Task HandleAsync_WhenRepeated_DoesNotIncreaseParticipantsCount()
    {
        var command = new UpsertAttendanceCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TransportMode.Walking);
        var modalSplit = ModalSplit(publicTransport: 48, walking: 19);
        var repository = new Mock<IAttendanceRepository>();
        repository
            .SetupSequence(x => x.UpsertAsync(
                It.Is<AttendanceIntent>(a => a.EventId == command.EventId && a.UserId == command.UserId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceUpsertPersistenceResult(true, 83, modalSplit))
            .ReturnsAsync(new AttendanceUpsertPersistenceResult(false, 83, modalSplit));
        var handler = CreateHandler(repository.Object);

        var first = await handler.HandleAsync(command);
        var repeated = await handler.HandleAsync(command);

        first.IsNew.Should().BeTrue();
        repeated.IsNew.Should().BeFalse();
        repeated.ParticipantsCount.Should().Be(first.ParticipantsCount);
        repeated.ModalSplit.Should().BeEquivalentTo(first.ModalSplit);
        repository.Verify(x => x.UpsertAsync(
            It.IsAny<AttendanceIntent>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_WhenTransportModeChanges_ReturnsUpdatedModalSplitWithoutIncreasingCount()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var repository = new Mock<IAttendanceRepository>();
        repository
            .SetupSequence(x => x.UpsertAsync(
                It.Is<AttendanceIntent>(a => a.EventId == eventId && a.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceUpsertPersistenceResult(
                true,
                83,
                ModalSplit(publicTransport: 48, walking: 19)))
            .ReturnsAsync(new AttendanceUpsertPersistenceResult(
                false,
                83,
                ModalSplit(publicTransport: 49, walking: 18)));
        var handler = CreateHandler(repository.Object);

        var initial = await handler.HandleAsync(
            new UpsertAttendanceCommand(eventId, userId, TransportMode.Walking));
        var updated = await handler.HandleAsync(
            new UpsertAttendanceCommand(eventId, userId, TransportMode.PublicTransport));

        updated.IsNew.Should().BeFalse();
        updated.ParticipantsCount.Should().Be(initial.ParticipantsCount);
        updated.ModalSplit[TransportMode.Walking].Should().Be(18);
        updated.ModalSplit[TransportMode.PublicTransport].Should().Be(49);
    }

    private static UpsertAttendanceHandler CreateHandler(IAttendanceRepository repository) =>
        new(repository, new FixedTimeProvider(Now));

    private static IReadOnlyDictionary<TransportMode, int> ModalSplit(
        int publicTransport,
        int walking) => new Dictionary<TransportMode, int>
        {
            [TransportMode.PublicTransport] = publicTransport,
            [TransportMode.Walking] = walking,
            [TransportMode.Bike] = 6,
            [TransportMode.Car] = 7,
            [TransportMode.Unknown] = 3
        };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
