using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Events.CreateEvent;
using FlowBB.Domain.Events;
using FluentAssertions;

namespace FlowBB.Application.Tests.Events;

public sealed class CreateEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_CreatesExternalEventWithServerGeneratedIdsAndZeroParticipants()
    {
        var writer = new CapturingEventWriter();
        var handler = new CreateEventHandler(writer);
        var command = new CreateEventCommand(
            "FlowBB Demo Event",
            "Event added live from the organizer dashboard.",
            new DateTimeOffset(2026, 9, 20, 19, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 9, 20, 22, 0, 0, TimeSpan.FromHours(2)),
            "Plac Bolesława Chrobrego",
            EventCategory.Community,
            49.8215,
            19.0455);

        var result = await handler.HandleAsync(command);

        result.IsValid.Should().BeTrue();
        result.Details!.ParticipantsCount.Should().Be(0);
        result.Details.Event.Source.Should().Be(EventSource.External);
        result.Details.Event.Id.Should().NotBe(Guid.Empty);
        writer.Event.Should().BeSameAs(result.Details.Event);
        writer.VenueId.Should().StartWith("external-venue-");
    }

    [Fact]
    public async Task HandleAsync_WithInvalidDomainData_DoesNotWrite()
    {
        var writer = new CapturingEventWriter();
        var handler = new CreateEventHandler(writer);
        var command = new CreateEventCommand(
            "",
            "",
            DateTimeOffset.UtcNow,
            null,
            "Venue",
            EventCategory.Other,
            49.8215,
            19.0455);

        var result = await handler.HandleAsync(command);

        result.IsValid.Should().BeFalse();
        writer.Event.Should().BeNull();
    }

    private sealed class CapturingEventWriter : IEventWriter
    {
        public Event? Event { get; private set; }

        public string? VenueId { get; private set; }

        public Task CreateAsync(
            Event @event,
            string venueId,
            CancellationToken cancellationToken = default)
        {
            Event = @event;
            VenueId = venueId;
            return Task.CompletedTask;
        }
    }
}
