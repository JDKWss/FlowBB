using FlowBB.Domain.Common;
using FlowBB.Domain.Routing;
using FluentAssertions;

namespace FlowBB.Domain.Tests.Routing;

public class RouteModelTests
{
    private static readonly DateTimeOffset Departure = new(2026, 9, 25, 18, 0, 0, TimeSpan.FromHours(2));
    private static readonly RouteStep Step = new(RouteStepType.Walk, "Idz 5 minut.", 5);
    private static readonly GeoPoint Point = new(49.82245, 19.04431);

    [Fact]
    public void RouteStep_WithValidData_CreatesStepWithOptionalLine()
    {
        var step = new RouteStep(RouteStepType.Transit, "Autobus.", 19, "7");

        step.Line.Should().Be("7");
        new RouteStep(RouteStepType.Walk, "Idz.", 0).Line.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void RouteStep_WithBlankInstruction_Throws(string instruction)
    {
        var act = () => new RouteStep(RouteStepType.Walk, instruction, 1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RouteStep_WithInstructionAtAndOverLimit()
    {
        new RouteStep(RouteStepType.Walk, new string('a', RouteStep.InstructionMaxLength), 1)
            .Instruction.Should().HaveLength(RouteStep.InstructionMaxLength);

        var act = () => new RouteStep(RouteStepType.Walk, new string('a', RouteStep.InstructionMaxLength + 1), 1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RouteStep_WithNegativeDurationOrLongLineOrUnknownType_Throws()
    {
        var negative = () => new RouteStep(RouteStepType.Walk, "x", -1);
        var longLine = () => new RouteStep(RouteStepType.Transit, "x", 1, new string('a', RouteStep.LineMaxLength + 1));
        var unknownType = () => new RouteStep((RouteStepType)99, "x", 1);

        negative.Should().Throw<ArgumentOutOfRangeException>();
        longLine.Should().Throw<ArgumentOutOfRangeException>();
        unknownType.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void JourneyOption_WithValidData_CreatesJourney()
    {
        var journey = new JourneyOption(37, Departure, Departure.AddMinutes(37), [Step]);

        journey.DurationMinutes.Should().Be(37);
        journey.Steps.Should().ContainSingle();
    }

    [Fact]
    public void RouteGeometry_WithValidLongitudeLatitudeCoordinates_CreatesLineString()
    {
        var geometry = new RouteGeometry([
            new RouteCoordinate(19.03384, 49.81272),
            new RouteCoordinate(19.04431, 49.82245)
        ]);

        geometry.Type.Should().Be("LineString");
        geometry.Coordinates.Should().HaveCount(2);
    }

    [Fact]
    public void RouteGeometry_RejectsTooFewOrInvalidCoordinates()
    {
        var tooShort = () => new RouteGeometry([new RouteCoordinate(19, 49)]);
        var invalidLongitude = () => new RouteCoordinate(double.PositiveInfinity, 49);
        var invalidLatitude = () => new RouteCoordinate(19, 91);

        tooShort.Should().Throw<ArgumentException>();
        invalidLongitude.Should().Throw<ArgumentOutOfRangeException>();
        invalidLatitude.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void JourneyOption_WithoutStepsOrWithArrivalBeforeDeparture_Throws()
    {
        var noSteps = () => new JourneyOption(1, Departure, Departure.AddMinutes(1), []);
        var backwards = () => new JourneyOption(1, Departure, Departure.AddMinutes(-1), [Step]);
        var negative = () => new JourneyOption(-1, Departure, Departure, [Step]);

        noSteps.Should().Throw<ArgumentException>();
        backwards.Should().Throw<ArgumentException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RoutePlan_WithEmptyReturnsAndGap_IsAllowed()
    {
        var outbound = new JourneyOption(5, Departure, Departure.AddMinutes(5), [Step]);

        var plan = new RoutePlan(PlannerSource.Demo, outbound, [], true);

        plan.Returns.Should().BeEmpty();
        plan.ReturnGap.Should().BeTrue();
    }

    [Fact]
    public void RoutePlan_WithUnknownSourceOrNullOutbound_Throws()
    {
        var outbound = new JourneyOption(5, Departure, Departure.AddMinutes(5), [Step]);

        var unknown = () => new RoutePlan((PlannerSource)99, outbound, [], false);
        var nullOutbound = () => new RoutePlan(PlannerSource.Demo, null!, [], false);

        unknown.Should().Throw<ArgumentOutOfRangeException>();
        nullOutbound.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RouteRequest_WithValidData_CreatesRequestWithOptionalEnd()
    {
        var request = new RouteRequest(Guid.NewGuid(), Departure, null, Point, Point, TransportMode.Car);

        request.EventEndAt.Should().BeNull();
        request.Mode.Should().Be(TransportMode.Car);
    }

    [Fact]
    public void RouteRequest_WithEmptyIdEndBeforeStartOrUnknownMode_Throws()
    {
        var emptyId = () => new RouteRequest(Guid.Empty, Departure, null, Point, Point, TransportMode.Car);
        var endBefore = () => new RouteRequest(Guid.NewGuid(), Departure, Departure.AddMinutes(-1), Point, Point, TransportMode.Car);
        var undefinedMode = () => new RouteRequest(Guid.NewGuid(), Departure, null, Point, Point, (TransportMode)99);

        emptyId.Should().Throw<ArgumentException>();
        endBefore.Should().Throw<ArgumentException>();
        undefinedMode.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RouteRequest_WithUnknownMode_Throws()
    {
        var act = () => new RouteRequest(Guid.NewGuid(), Departure, null, Point, Point, TransportMode.Unknown);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("mode");
    }

    [Fact]
    public void GeoDistance_ForIdenticalPoints_IsZero()
    {
        GeoDistance.KilometersBetween(Point, Point).Should().Be(0);
    }

    [Fact]
    public void GeoDistance_ForOneDegreeOfLatitude_IsAbout111Kilometers()
    {
        GeoDistance.KilometersBetween(new GeoPoint(49, 19), new GeoPoint(50, 19)).Should().BeApproximately(111.2, 0.2);
    }

    [Fact]
    public void GeoDistance_IsSymmetric()
    {
        var other = new GeoPoint(49.798, 19.08);

        GeoDistance.KilometersBetween(Point, other).Should().Be(GeoDistance.KilometersBetween(other, Point));
    }

    [Fact]
    public void EnumNames_MatchOpenApiContract()
    {
        Enum.GetNames<RouteStepType>().Should().Equal("Walk", "Transit", "Bike", "Car", "Wait");
        Enum.GetNames<PlannerSource>().Should().Equal("Demo", "RoadRouting", "OpenTripPlanner", "MzkTimetable");
    }
}
