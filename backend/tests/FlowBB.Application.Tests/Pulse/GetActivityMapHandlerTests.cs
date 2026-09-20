using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Pulse;
using FlowBB.Application.Pulse.GetActivityMap;
using FlowBB.Domain.Common;
using FluentAssertions;
using Moq;

namespace FlowBB.Application.Tests.Pulse;

public class GetActivityMapHandlerTests
{
    private const double RynekLatitude = 49.8225;
    private const double RynekLongitude = 19.0444;
    private const double CoordinateTolerance = 1e-9;
    private const double FarLatitude = 49.7800;
    private const double FarLongitude = 19.1200;

    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static List<PulsePoint> Points(int count, TransportMode mode, double latitude, double longitude) =>
        Enumerable.Range(0, count).Select(_ => new PulsePoint(latitude, longitude, mode)).ToList();

    private static async Task<ActivityMap> RunAsync(IReadOnlyList<PulsePoint> points)
    {
        var reader = new Mock<IPulseDataReader>();
        reader.Setup(r => r.GetPointsAsync(EventId, It.IsAny<CancellationToken>())).ReturnsAsync(points);
        return await new GetActivityMapHandler(reader.Object).HandleAsync(EventId);
    }

    [Theory]
    [InlineData(9, 0)]
    [InlineData(10, 1)]
    [InlineData(11, 1)]
    public async Task Cell_IsReturnedOnlyWhenItHasAtLeastTenParticipants(int participants, int expectedCells)
    {
        var map = await RunAsync(Points(participants, TransportMode.Walking, RynekLatitude, RynekLongitude));

        map.Cells.Should().HaveCount(expectedCells);
        map.ParticipantsCount.Should().Be(participants);
    }

    [Fact]
    public async Task ReturnedCell_ContainsParticipantsAndModalSplit()
    {
        var points = Points(6, TransportMode.PublicTransport, RynekLatitude, RynekLongitude)
            .Concat(Points(3, TransportMode.Walking, RynekLatitude, RynekLongitude))
            .Concat(Points(2, TransportMode.Car, RynekLatitude, RynekLongitude))
            .ToList();

        var map = await RunAsync(points);

        var cell = map.Cells.Should().ContainSingle().Subject;
        cell.Participants.Should().Be(11);
        cell.ModalSplit.Should().Be(new ModalSplit(PublicTransport: 6, Walking: 3, Bike: 0, Car: 2, Unknown: 0));
        cell.Vertices.Should().HaveCount(6);
    }

    [Fact]
    public async Task SmallCell_IsHiddenButStillCountedInTotals()
    {
        var points = Points(10, TransportMode.Bike, RynekLatitude, RynekLongitude)
            .Concat(Points(4, TransportMode.Car, FarLatitude, FarLongitude))
            .ToList();

        var map = await RunAsync(points);

        map.Cells.Should().ContainSingle().Which.Participants.Should().Be(10);
        map.ParticipantsCount.Should().Be(14);
        map.ModalSplit.Should().Be(new ModalSplit(0, 0, 10, 4, 0));
    }

    [Fact]
    public async Task PointsFromDifferentAreas_ProduceSeparateCells()
    {
        var points = Points(10, TransportMode.Walking, RynekLatitude, RynekLongitude)
            .Concat(Points(12, TransportMode.Car, FarLatitude, FarLongitude))
            .ToList();

        var map = await RunAsync(points);

        map.Cells.Should().HaveCount(2);
        map.Cells.Select(c => c.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task NoPoints_ReturnsEmptyMap()
    {
        var map = await RunAsync([]);

        map.ParticipantsCount.Should().Be(0);
        map.ModalSplit.Should().Be(new ModalSplit(0, 0, 0, 0, 0));
        map.Cells.Should().BeEmpty();
    }

    [Fact]
    public async Task Result_IsDeterministicRegardlessOfInputOrder()
    {
        var points = Points(10, TransportMode.Walking, RynekLatitude, RynekLongitude)
            .Concat(Points(10, TransportMode.Car, FarLatitude, FarLongitude))
            .ToList();
        var reversed = Enumerable.Reverse(points).ToList();

        var first = await RunAsync(points);
        var second = await RunAsync(reversed);

        second.Should().BeEquivalentTo(first, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Result_DoesNotExposeUserIdentifiersOrExactCoordinates()
    {
        var map = await RunAsync(Points(10, TransportMode.Walking, RynekLatitude, RynekLongitude));

        var exposed = typeof(ActivityMap).GetProperties().Concat(typeof(HexCell).GetProperties())
            .Select(property => property.Name);

        exposed.Should().NotContain(["UserId", "Latitude", "Longitude", "Points"]);
        map.Cells.Single().Vertices.Should().NotContain(v =>
            Math.Abs(v.Latitude - RynekLatitude) < CoordinateTolerance && Math.Abs(v.Longitude - RynekLongitude) < CoordinateTolerance);
    }

    [Fact]
    public async Task EmptyEventId_Throws()
    {
        var handler = new GetActivityMapHandler(Mock.Of<IPulseDataReader>());

        var act = () => handler.HandleAsync(Guid.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
