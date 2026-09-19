namespace FlowBB.Domain.Models;

public sealed record BusinessOwner(
    string OwnerId,
    string CompanyName,
    string Email,
    bool IsVerified);
