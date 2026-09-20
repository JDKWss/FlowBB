using FlowBB.Domain.Crews;
using FluentAssertions;

namespace FlowBB.Domain.Tests.Crews;

public class CrewTests
{
    private static readonly MeetingPoint Point = new("Rynek", 49.82, 19.04);

    private static Crew NewCrew(
        int maxMembers = 3,
        Guid? eventId = null,
        IEnumerable<string>? tags = null) =>
        Crew.Create(Guid.NewGuid(), eventId ?? Guid.NewGuid(), "Ekipa", "Opis", maxMembers, tags, Point);

    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var crew = Crew.Create(id, eventId, " Ekipa ", null, 4, ["muzyka"], Point);

        crew.Id.Should().Be(id);
        crew.EventId.Should().Be(eventId);
        crew.Name.Should().Be("Ekipa");
        crew.Description.Should().BeEmpty();
        crew.MaxMembers.Should().Be(4);
        crew.Tags.Should().Equal("muzyka");
        crew.MeetingPoint.Should().Be(Point);
        crew.CurrentMembers.Should().Be(0);
    }

    [Fact]
    public void Create_WithEmptyEventId_Throws()
    {
        var act = () => NewCrew(eventId: Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyId_Throws()
    {
        var act = () => Crew.Create(Guid.Empty, Guid.NewGuid(), "Ekipa", "", 4, null, Point);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_Throws(string name)
    {
        var act = () => Crew.Create(Guid.NewGuid(), Guid.NewGuid(), name, "", 4, null, Point);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithTooLongNameOrDescription_Throws()
    {
        var longName = () => Crew.Create(Guid.NewGuid(), Guid.NewGuid(), new string('a', 101), "", 4, null, Point);
        var longDescription = () => Crew.Create(Guid.NewGuid(), Guid.NewGuid(), "n", new string('a', 501), 4, null, Point);

        longName.Should().Throw<ArgumentOutOfRangeException>();
        longDescription.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(13)]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_WithInvalidMaxMembers_Throws(int maxMembers)
    {
        var act = () => NewCrew(maxMembers);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(12)]
    public void Create_WithBoundaryMaxMembers_Succeeds(int maxMembers)
    {
        NewCrew(maxMembers).MaxMembers.Should().Be(maxMembers);
    }

    [Theory]
    [InlineData(90.1, 0)]
    [InlineData(-90.1, 0)]
    [InlineData(0, 180.1)]
    [InlineData(0, -180.1)]
    public void MeetingPoint_WithCoordinatesOutOfRange_Throws(double latitude, double longitude)
    {
        var act = () => new MeetingPoint("Rynek", latitude, longitude);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void MeetingPoint_WithBlankName_Throws(string name)
    {
        var act = () => new MeetingPoint(name, 0, 0);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MeetingPoint_WithTooLongName_Throws()
    {
        var act = () => new MeetingPoint(new string('a', 121), 0, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MeetingPoint_WithBoundaryValues_Succeeds()
    {
        var act = () => new MeetingPoint(new string('a', 120), -90, 180);

        act.Should().NotThrow();
    }

    [Fact]
    public void Join_NewUser_AddsMember()
    {
        var crew = NewCrew();
        var userId = Guid.NewGuid();

        var result = crew.Join(userId);

        result.Should().Be(JoinCrewResult.Joined);
        crew.CurrentMembers.Should().Be(1);
        crew.HasMember(userId).Should().BeTrue();
    }

    [Fact]
    public void Join_SameUserAgain_IsIdempotent()
    {
        var crew = NewCrew();
        var userId = Guid.NewGuid();
        crew.Join(userId);

        var result = crew.Join(userId);

        result.Should().Be(JoinCrewResult.AlreadyMember);
        crew.CurrentMembers.Should().Be(1);
    }

    [Fact]
    public void Join_FullCrew_ReturnsFullAndDoesNotAdd()
    {
        var crew = NewCrew(maxMembers: 2);
        crew.Join(Guid.NewGuid());
        crew.Join(Guid.NewGuid());
        var extra = Guid.NewGuid();

        var result = crew.Join(extra);

        result.Should().Be(JoinCrewResult.Full);
        crew.CurrentMembers.Should().Be(2);
        crew.HasMember(extra).Should().BeFalse();
    }

    [Fact]
    public void Join_ExistingMemberOfFullCrew_ReturnsAlreadyMember()
    {
        var crew = NewCrew(maxMembers: 2);
        var userId = Guid.NewGuid();
        crew.Join(userId);
        crew.Join(Guid.NewGuid());

        crew.Join(userId).Should().Be(JoinCrewResult.AlreadyMember);
    }

    [Fact]
    public void Join_WithEmptyUserId_Throws()
    {
        var act = () => NewCrew().Join(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Leave_ExistingMember_RemovesMember()
    {
        var crew = NewCrew();
        var userId = Guid.NewGuid();
        crew.Join(userId);

        crew.Leave(userId).Should().BeTrue();

        crew.CurrentMembers.Should().Be(0);
        crew.HasMember(userId).Should().BeFalse();
    }

    [Fact]
    public void Leave_Twice_IsSafeAndCountStaysNonNegative()
    {
        var crew = NewCrew();
        var userId = Guid.NewGuid();
        crew.Join(userId);
        crew.Leave(userId);

        crew.Leave(userId).Should().BeFalse();
        crew.Leave(Guid.NewGuid()).Should().BeFalse();

        crew.CurrentMembers.Should().Be(0);
    }

    [Fact]
    public void Leave_FreesPlaceForAnotherUser()
    {
        var crew = NewCrew(maxMembers: 2);
        var first = Guid.NewGuid();
        crew.Join(first);
        crew.Join(Guid.NewGuid());
        crew.Leave(first);

        crew.Join(Guid.NewGuid()).Should().Be(JoinCrewResult.Joined);
    }

    [Fact]
    public void CurrentMembers_ReflectsMembersCollection()
    {
        var crew = NewCrew(maxMembers: 5);
        crew.Join(Guid.NewGuid());
        crew.Join(Guid.NewGuid());
        var third = Guid.NewGuid();
        crew.Join(third);
        crew.Leave(third);

        crew.CurrentMembers.Should().Be(2).And.Be(crew.Members.Count);
    }

    [Fact]
    public void Members_CannotBeModifiedFromOutside()
    {
        var crew = NewCrew();
        crew.Join(Guid.NewGuid());

        var members = (ICollection<Guid>)crew.Members;
        var act = () => members.Add(Guid.NewGuid());

        act.Should().Throw<NotSupportedException>();
        crew.CurrentMembers.Should().Be(1);
    }

    [Fact]
    public void Tags_CannotBeModifiedFromOutside()
    {
        var crew = NewCrew(tags: ["a"]);

        var tags = (ICollection<string>)crew.Tags;
        var act = () => tags.Add("b");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Tags_AreTrimmed()
    {
        var crew = NewCrew(tags: ["  muzyka ", "rower\t"]);

        crew.Tags.Should().Equal("muzyka", "rower");
    }

    [Fact]
    public void Tags_WithDuplicatesAfterNormalization_Throw()
    {
        var act = () => NewCrew(tags: ["rower", " Rower "]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tags_WithBlankTag_Throw()
    {
        var act = () => NewCrew(tags: ["ok", "   "]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tags_LimitsAreEnforced()
    {
        var tooMany = () => NewCrew(tags: Enumerable.Range(0, 11).Select(i => $"t{i}"));
        var tooLong = () => NewCrew(tags: [new string('a', 41)]);
        var atLimit = () => NewCrew(tags: Enumerable.Range(0, 10).Select(i => new string((char)('a' + i), 40)));

        tooMany.Should().Throw<ArgumentOutOfRangeException>();
        tooLong.Should().Throw<ArgumentOutOfRangeException>();
        atLimit.Should().NotThrow();
    }
}
