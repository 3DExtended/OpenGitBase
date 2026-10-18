using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.AccountSecurity.Entities;

namespace OpenGitBase.Features.AccountSecurity.Challenges;

/// <summary>
/// Single-use challenges in the database, so every API replica sees the same state. Only hashes are
/// stored. Expiry is compared in memory: SQLite, used by the tests, cannot compare DateTimeOffset.
/// </summary>
internal static class AuthenticationChallenges
{
    /// <summary>
    /// Stores a challenge bound to <paramref name="binding"/>, or renews the one already bound to it,
    /// and drops the user's expired ones.
    /// </summary>
    public static async Task CreateAsync(
        OpenGitBaseDbContext context,
        AuthenticationChallengePurpose purpose,
        Guid userId,
        string binding,
        TimeSpan lifetime,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        string? payload = null
    )
    {
        var tokenHash = Hash(binding);
        var existing = await context
            .Set<AuthenticationChallengeEntity>()
            .Where(c => c.UserId == userId)
            .ToListAsync(cancellationToken);
        var renewed = existing.FirstOrDefault(c => c.Purpose == purpose && c.TokenHash == tokenHash);
        context
            .Set<AuthenticationChallengeEntity>()
            .RemoveRange(existing.Where(c => c != renewed && c.ExpiresAt <= now));

        if (renewed is null)
        {
            context.Set<AuthenticationChallengeEntity>().Add(
                new AuthenticationChallengeEntity
                {
                    Id = Guid.NewGuid(),
                    Purpose = purpose,
                    UserId = userId,
                    TokenHash = tokenHash,
                    Payload = payload,
                    ExpiresAt = now + lifetime,
                    CreatedAt = now,
                }
            );
        }
        else
        {
            renewed.ExpiresAt = now + lifetime;
            renewed.CreatedAt = now;
            renewed.Payload = payload;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The unexpired challenge bound to <paramref name="binding"/>, or null.</summary>
    public static async Task<AuthenticationChallengeEntity?> FindActiveAsync(
        OpenGitBaseDbContext context,
        AuthenticationChallengePurpose purpose,
        string binding,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var tokenHash = Hash(binding);
        var challenge = await context
            .Set<AuthenticationChallengeEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Purpose == purpose && c.TokenHash == tokenHash, cancellationToken);
        return challenge is not null && challenge.ExpiresAt > now ? challenge : null;
    }

    /// <summary>
    /// Deletes the challenge and reports whether this call did it, so of two concurrent requests
    /// answering the same challenge only one succeeds.
    /// </summary>
    public static async Task<bool> TryConsumeAsync(
        OpenGitBaseDbContext context,
        Guid challengeId,
        CancellationToken cancellationToken
    ) =>
        await context
            .Set<AuthenticationChallengeEntity>()
            .Where(c => c.Id == challengeId)
            .ExecuteDeleteAsync(cancellationToken) == 1;

    /// <summary>A random 256-bit token for the client, base64url without padding.</summary>
    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string binding) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(binding)));
}
