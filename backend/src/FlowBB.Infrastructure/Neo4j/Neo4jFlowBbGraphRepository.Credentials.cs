using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;

namespace FlowBB.Infrastructure.Neo4j;

public sealed partial class Neo4jFlowBbGraphRepository
{
    private static readonly PasswordHasher<string> PasswordHasher = new();
    private static readonly string DummyPasswordHash = PasswordHasher.HashPassword(
        string.Empty, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    public Task<bool> SetUserPasswordAsync(Guid userId, string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length is < 12 or > 1024)
            throw new ArgumentException("Password must contain between 12 and 1024 characters.", nameof(password));
        var id = ToDatabaseId(userId);
        var hash = PasswordHasher.HashPassword(id, password);
        const string query = """
            MATCH (u:User {UserId: $UserId}) SET u.PasswordHash = $Hash
            RETURN count(u) = 1 AS Success
            """;
        return ExecuteBooleanAsync(query, new { UserId = id, Hash = hash });
    }

    public async Task<bool> VerifyLoginAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password) || password.Length > 1024)
            return false;
        const string query = """
            MATCH (u:User) WHERE toLower(trim(u.Email)) = $Email
            RETURN u.UserId AS UserId, u.PasswordHash AS Hash LIMIT 2
            """;
        var credentials = await ExecuteListAsync(query, new { Email = email.Trim().ToLowerInvariant() },
            record => new StoredCredential(record.Get<string>("UserId"), record.Get<string?>("Hash")));
        var credential = credentials.Count == 1 ? credentials[0] : null;
        var result = VerifyStoredHash(credential?.UserId ?? string.Empty,
            credential?.Hash ?? DummyPasswordHash, password);
        if (credential?.Hash is null || result == PasswordVerificationResult.Failed)
            return false;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            await UpgradePasswordHashAsync(credential, password);
        return true;
    }

    private static PasswordVerificationResult VerifyStoredHash(string userId, string hash, string password)
    {
        try
        {
            return PasswordHasher.VerifyHashedPassword(userId, hash, password);
        }
        catch (FormatException)
        {
            // Synthetic seed placeholders and corrupt hashes must never authenticate.
            PasswordHasher.VerifyHashedPassword(userId, DummyPasswordHash, password);
            return PasswordVerificationResult.Failed;
        }
    }

    private Task UpgradePasswordHashAsync(StoredCredential credential, string password)
    {
        const string query = """
            MATCH (u:User {UserId: $UserId}) WHERE u.PasswordHash = $OldHash
            SET u.PasswordHash = $NewHash
            """;
        return ExecuteAsync(query, new
        {
            credential.UserId, OldHash = credential.Hash,
            NewHash = PasswordHasher.HashPassword(credential.UserId, password)
        });
    }

    private sealed record StoredCredential(string UserId, string? Hash);
}
