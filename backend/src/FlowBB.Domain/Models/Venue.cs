namespace FlowBB.Domain.Models;

public sealed record Venue(
    string VenueId,
    string Name,
    string Address,
    double Latitude,
    double Longitude);
