using FlowBB.Application.Pulse.GetActivityMap;
using FluentAssertions;

namespace FlowBB.Application.Tests.Pulse;

public class HexGridTests
{
    private const double Latitude = 49.8225;
    private const double Longitude = 19.0444;

    [Fact]
    public void SamePoint_AlwaysMapsToSameCell()
    {
        var first = HexGrid.CellAt(Latitude, Longitude);
        var second = HexGrid.CellAt(Latitude, Longitude);

        second.Should().Be(first);
        second.Id.Should().Be(first.Id);
    }

    [Fact]
    public void PointsMoreThanOneCellApart_MapToDifferentCells()
    {
        var near = HexGrid.CellAt(Latitude, Longitude);
        var far = HexGrid.CellAt(Latitude + 0.02, Longitude);

        far.Should().NotBe(near);
    }

    [Fact]
    public void Cell_HasSixVerticesAroundItsCenter()
    {
        var cell = HexGrid.CellAt(Latitude, Longitude);

        var vertices = HexGrid.VerticesOf(cell);

        vertices.Should().HaveCount(6);
        var centerLatitude = vertices.Average(v => v.Latitude);
        var centerLongitude = vertices.Average(v => v.Longitude);
        HexGrid.CellAt(centerLatitude, centerLongitude).Should().Be(cell);
    }

    [Fact]
    public void CellWidth_MatchesRequestedSizeInMeters()
    {
        var vertices = HexGrid.VerticesOf(new HexCoordinate(0, 0), 900);

        // Pointy-top: wierzcholki 0 i 1 leza na jednej pionowej krawedzi, wiec odleglosc
        // poziomych srodkow przeciwleglych krawedzi to szerokosc komorki (plaska).
        var centerLongitude = vertices.Average(v => v.Longitude);
        var rightEdge = (vertices[0].Longitude + vertices[1].Longitude) / 2;
        var halfWidthMeters = (rightEdge - centerLongitude) * 111_195 * Math.Cos(Latitude * Math.PI / 180);

        (halfWidthMeters * 2).Should().BeApproximately(900, 20);
    }

    [Fact]
    public void NonPositiveWidth_Throws()
    {
        var act = () => HexGrid.CellAt(Latitude, Longitude, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
