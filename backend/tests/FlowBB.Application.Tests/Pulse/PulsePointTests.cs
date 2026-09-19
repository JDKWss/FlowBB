using FlowBB.Application.Pulse;
using FlowBB.Domain.Common;
using FluentAssertions;

namespace FlowBB.Application.Tests.Pulse;

public class PulsePointTests
{
    [Theory]
    [InlineData(90.1, 0)]
    [InlineData(-90.1, 0)]
    [InlineData(0, 180.1)]
    [InlineData(0, -180.1)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    public void InvalidCoordinates_Throw(double latitude, double longitude)
    {
        var act = () => new PulsePoint(latitude, longitude, TransportMode.Walking);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BoundaryCoordinates_AreAccepted()
    {
        var act = () => new PulsePoint(-90, 180, TransportMode.Unknown);

        act.Should().NotThrow();
    }
}
