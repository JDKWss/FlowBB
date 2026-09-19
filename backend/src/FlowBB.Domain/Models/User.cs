namespace FlowBB.Domain.Models;

public sealed record User(
    string UserId,
    string Email,
    string PasswordHash,
    string Name);
