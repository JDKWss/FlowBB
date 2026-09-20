using FlowBB.Domain.Common;
using FluentAssertions;

namespace FlowBB.Domain.Tests.Common;

public class GeoPointTests
{
    [Theory]
    [InlineData(49.82245, 19.04431)]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    public void Constructor_WithCoordinatesInRange_CreatesPoint(double latitude, double longitude)
    {
        var point = new GeoPoint(latitude, longitude);

        point.Latitude.Should().Be(latitude);
        point.Longitude.Should().Be(longitude);
    }

    [Theory]
    [InlineData(90.0001, 0)]
    [InlineData(-90.0001, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    public void Constructor_WithInvalidLatitude_Throws(double latitude, double longitude)
    {
        var act = () => new GeoPoint(latitude, longitude);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("latitude");
    }

    [Theory]
    [InlineData(0, 180.0001)]
    [InlineData(0, -180.0001)]
    [InlineData(0, double.NaN)]
    [InlineData(0, double.NegativeInfinity)]
    public void Constructor_WithInvalidLongitude_Throws(double latitude, double longitude)
    {
        var act = () => new GeoPoint(latitude, longitude);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("longitude");
    }

    [Fact]
    public void Equality_ComparesCoordinates()
    {
        new GeoPoint(49.8, 19.0).Should().Be(new GeoPoint(49.8, 19.0));
    }
}
