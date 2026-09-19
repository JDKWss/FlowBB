namespace FlowBB.Domain.Models;

public sealed record User(
    Guid UserId,
    string Email,
    string PasswordHash,
    string Name,
    double DefaultOriginLatitude,
    double DefaultOriginLongitude);
