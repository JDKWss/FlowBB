using FluentAssertions;
using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Attendance.UpsertAttendance;
using FlowBB.Application.Pulse;
using FlowBB.Domain.Attendance;
using FlowBB.Domain.Common;
using Moq;

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
        var modalSplit = CreateModalSplit(publicTransport: 49, walking: 18);
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
        savedAttendance.EventId.Should().Be(eventId);
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
            modalSplit,
            ParticipantsWithoutReturn: 0));
        repository.VerifyAll();
    }

    [Theory]
    [InlineData(2026, 9, 25, 20, 0, 49)] // 22:00 w Warszawie (UTC+2) -> pozny koniec, licza sie osoby z PublicTransport
    [InlineData(2026, 9, 25, 19, 59, 0)] // 21:59 w Warszawie -> brak luki
    public async Task HandleAsync_ComputesParticipantsWithoutReturnFromEventEnd(
        int year, int month, int day, int hour, int minute, int expected)
    {
        var eventId = Guid.NewGuid();
        var endAt = new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);
        var repository = new Mock<IAttendanceRepository>();
        repository
            .Setup(x => x.UpsertAsync(It.IsAny<AttendanceIntent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceUpsertPersistenceResult(true, 83, CreateModalSplit(publicTransport: 49, walking: 18)));
        var handler = CreateHandler(repository.Object, PulseReaderWithEnd(eventId, endAt));

        var result = await handler.HandleAsync(new UpsertAttendanceCommand(eventId, Guid.NewGuid(), TransportMode.Walking));

        result!.ParticipantsWithoutReturn.Should().Be(expected);
    }

    [Fact]
    public async Task HandleAsync_WhenEventHasNoEndOrInfo_ReportsNoReturnGap()
    {
        var repository = new Mock<IAttendanceRepository>();
        repository
            .Setup(x => x.UpsertAsync(It.IsAny<AttendanceIntent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceUpsertPersistenceResult(true, 83, CreateModalSplit(publicTransport: 49, walking: 18)));
        var handler = CreateHandler(repository.Object);

        var result = await handler.HandleAsync(
            new UpsertAttendanceCommand(Guid.NewGuid(), Guid.NewGuid(), TransportMode.PublicTransport));

        result!.ParticipantsWithoutReturn.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_WhenRepeated_DoesNotIncreaseParticipantsCount()
    {
        var command = new UpsertAttendanceCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TransportMode.Walking);
        var modalSplit = CreateModalSplit(publicTransport: 48, walking: 19);
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

        first.Should().NotBeNull();
        repeated.Should().NotBeNull();
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
                CreateModalSplit(publicTransport: 48, walking: 19)))
            .ReturnsAsync(new AttendanceUpsertPersistenceResult(
                false,
                83,
                CreateModalSplit(publicTransport: 49, walking: 18)));
        var handler = CreateHandler(repository.Object);

        var initial = await handler.HandleAsync(
            new UpsertAttendanceCommand(eventId, userId, TransportMode.Walking));
        var updated = await handler.HandleAsync(
            new UpsertAttendanceCommand(eventId, userId, TransportMode.PublicTransport));

        initial.Should().NotBeNull();
        updated.Should().NotBeNull();
        updated.IsNew.Should().BeFalse();
        updated.ParticipantsCount.Should().Be(initial.ParticipantsCount);
        updated.ModalSplit.Walking.Should().Be(18);
        updated.ModalSplit.PublicTransport.Should().Be(49);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    public async Task HandleAsync_WithUndefinedTransportMode_ThrowsWithoutCallingRepository(int value)
    {
        var repository = new Mock<IAttendanceRepository>(MockBehavior.Strict);
        var handler = CreateHandler(repository.Object);
        var command = new UpsertAttendanceCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            (TransportMode)value);

        var action = () => handler.HandleAsync(command);

        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenEventOrUserDoesNotExist_ReturnsNull()
    {
        var repository = new Mock<IAttendanceRepository>(MockBehavior.Strict);
        repository
            .Setup(x => x.UpsertAsync(It.IsAny<AttendanceIntent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceUpsertPersistenceResult?)null);
        var handler = CreateHandler(repository.Object);

        var result = await handler.HandleAsync(
            new UpsertAttendanceCommand(Guid.NewGuid(), Guid.NewGuid(), TransportMode.Bike));

        result.Should().BeNull();
        repository.VerifyAll();
    }

    private static UpsertAttendanceHandler CreateHandler(
        IAttendanceRepository repository,
        IPulseDataReader? pulseReader = null) =>
        new(repository, pulseReader ?? Mock.Of<IPulseDataReader>(), new FixedTimeProvider(Now));

    private static IPulseDataReader PulseReaderWithEnd(Guid eventId, DateTimeOffset endAt)
    {
        var reader = new Mock<IPulseDataReader>();
        reader
            .Setup(x => x.GetEventAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PulseEventInfo(eventId, "Wydarzenie", endAt));
        return reader.Object;
    }

    private static ModalSplit CreateModalSplit(
        int publicTransport,
        int walking) => new(publicTransport, walking, Bike: 6, Car: 7, Unknown: 3);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
