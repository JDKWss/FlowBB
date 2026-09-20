using FlowBB.Domain.Common;
using FlowBB.Domain.Events;
using FluentAssertions;

namespace FlowBB.Domain.Tests.Events;

public class EventTests
{
    private static readonly Guid ValidId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Start = new(2026, 9, 25, 18, 0, 0, TimeSpan.FromHours(2));
    private static readonly GeoPoint Location = new(49.82245, 19.04431);

    [Fact]
    public void Constructor_WithEndAt_CreatesEvent()
    {
        var end = Start.AddHours(2);

        var result = CreateEvent(endAt: end);

        result.Id.Should().Be(ValidId);
        result.Name.Should().Be("Koncert na Rynku");
        result.StartAt.Should().Be(Start);
        result.EndAt.Should().Be(end);
        result.VenueName.Should().Be("Rynek");
        result.Location.Should().Be(Location);
    }

    [Fact]
    public void Constructor_WithoutEndAt_CreatesEvent()
    {
        var result = CreateEvent(endAt: null);

        result.EndAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithEndAtEqualToStartAt_CreatesEvent()
    {
        var result = CreateEvent(endAt: Start);

        result.EndAt.Should().Be(Start);
    }

    [Fact]
    public void Constructor_WithEmptyId_Throws()
    {
        var act = () => CreateEvent(id: Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("id");
    }

    [Fact]
    public void Constructor_WithEndAtBeforeStartAt_Throws()
    {
        var act = () => CreateEvent(endAt: Start.AddMinutes(-1));

        act.Should().Throw<ArgumentException>().WithParameterName("endAt");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_Throws(string name)
    {
        var act = () => CreateEvent(name: name);

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void Constructor_WithNameAtLimit_CreatesEvent()
    {
        var name = new string('a', Event.NameMaxLength);

        CreateEvent(name: name).Name.Should().Be(name);
    }

    [Fact]
    public void Constructor_WithNameOverLimit_Throws()
    {
        var act = () => CreateEvent(name: new string('a', Event.NameMaxLength + 1));

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void Constructor_WithEmptyDescription_CreatesEvent()
    {
        CreateEvent(description: string.Empty).Description.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithDescriptionOverLimit_Throws()
    {
        var act = () => CreateEvent(description: new string('a', Event.DescriptionMaxLength + 1));

        act.Should().Throw<ArgumentException>().WithParameterName("description");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankVenueName_Throws(string venueName)
    {
        var act = () => CreateEvent(venueName: venueName);

        act.Should().Throw<ArgumentException>().WithParameterName("venueName");
    }

    [Fact]
    public void Constructor_WithVenueNameOverLimit_Throws()
    {
        var act = () => CreateEvent(venueName: new string('a', Event.VenueNameMaxLength + 1));

        act.Should().Throw<ArgumentException>().WithParameterName("venueName");
    }

    [Theory]
    [InlineData(EventCategory.Culture)]
    [InlineData(EventCategory.Sport)]
    [InlineData(EventCategory.Education)]
    [InlineData(EventCategory.Community)]
    [InlineData(EventCategory.Other)]
    public void Constructor_WithKnownCategory_CreatesEvent(EventCategory category)
    {
        CreateEvent(category: category).Category.Should().Be(category);
    }

    [Theory]
    [InlineData(EventSource.Demo)]
    [InlineData(EventSource.City)]
    [InlineData(EventSource.External)]
    public void Constructor_WithKnownSource_CreatesEvent(EventSource source)
    {
        CreateEvent(source: source).Source.Should().Be(source);
    }

    [Fact]
    public void Constructor_WithUnknownCategory_Throws()
    {
        var act = () => CreateEvent(category: (EventCategory)999);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("category");
    }

    [Fact]
    public void Constructor_WithUnknownSource_Throws()
    {
        var act = () => CreateEvent(source: (EventSource)999);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("source");
    }

    [Fact]
    public void EnumNames_MatchOpenApiContract()
    {
        Enum.GetNames<EventCategory>().Should().Equal("Culture", "Sport", "Education", "Community", "Other");
        Enum.GetNames<EventSource>().Should().Equal("Demo", "City", "External");
    }

    private static Event CreateEvent(
        Guid? id = null,
        string name = "Koncert na Rynku",
        string description = "Wieczorny koncert.",
        DateTimeOffset? endAt = null,
        string venueName = "Rynek",
        EventCategory category = EventCategory.Culture,
        EventSource source = EventSource.Demo)
    {
        return new Event(id ?? ValidId, name, description, Start, endAt, venueName, category, source, Location);
    }
}
