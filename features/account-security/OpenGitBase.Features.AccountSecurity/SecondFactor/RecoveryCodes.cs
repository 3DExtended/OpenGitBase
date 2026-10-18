using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Features.AccountSecurity.Entities;

namespace OpenGitBase.Features.AccountSecurity.SecondFactor;

/// <summary>
/// Single-use recovery codes: 10 per set, each 10 Base32 characters shown as <c>xxxxx-xxxxx</c>.
/// Input is compared without dashes and spaces and in any case.
/// </summary>
internal static class RecoveryCodes
{
    public const int SetSize = 10;

    private const int CodeLength = 10;
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz234567";

    /// <summary>
    /// Replaces the user's set with a fresh one and returns the new codes in plain text, the only time
    /// they exist outside the user's hands. The caller saves the context.
    /// </summary>
    public static async Task<IReadOnlyList<string>> ReplaceSetAsync(
        OpenGitBaseDbContext context,
        IPasswordHasherService hasher,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        await RemoveSetAsync(context, userId, cancellationToken);
        var codes = new List<string>(SetSize);
        for (var i = 0; i < SetSize; i++)
        {
            var code = NewCode();
            codes.Add(code);
            context.Set<UserRecoveryCodeEntity>().Add(
                new UserRecoveryCodeEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CodeHash = hasher.HashPassword(Normalize(code)),
                    CreatedAt = now,
                }
            );
        }

        return codes;
    }

    /// <summary>
    /// Checks the input against the user's unused codes and spends the matching one with a conditional
    /// update, so a code is accepted once even under concurrent use.
    /// </summary>
    public static async Task<bool> TryUseAsync(
        OpenGitBaseDbContext context,
        IPasswordHasherService hasher,
        Guid userId,
        string input,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var normalized = Normalize(input);
        if (normalized.Length != CodeLength)
        {
            return false;
        }

        var unused = await context
            .Set<UserRecoveryCodeEntity>()
            .AsNoTracking()
            .Where(code => code.UserId == userId && code.UsedAt == null)
            .Select(code => new { code.Id, code.CodeHash })
            .ToListAsync(cancellationToken);
        var match = unused.FirstOrDefault(code => hasher.VerifyPassword(code.CodeHash, normalized));
        if (match is null)
        {
            return false;
        }

        return await context
            .Set<UserRecoveryCodeEntity>()
            .Where(code => code.Id == match.Id && code.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(code => code.UsedAt, now), cancellationToken) == 1;
    }

    public static Task<int> CountUnusedAsync(OpenGitBaseDbContext context, Guid userId, CancellationToken cancellationToken) =>
        context.Set<UserRecoveryCodeEntity>().CountAsync(code => code.UserId == userId && code.UsedAt == null, cancellationToken);

    public static async Task LogUseAsync(
        ILogger logger,
        OpenGitBaseDbContext context,
        Guid userId,
        string step,
        CancellationToken cancellationToken
    )
    {
        var remaining = await CountUnusedAsync(context, userId, cancellationToken);
        logger.LogWarning(
            "Recovery code used by user {UserId} during {Step}; {Remaining} unused codes left",
            userId,
            step,
            remaining
        );
    }

    /// <summary>Marks every code of the user for deletion; the caller saves the context.</summary>
    public static async Task RemoveSetAsync(OpenGitBaseDbContext context, Guid userId, CancellationToken cancellationToken)
    {
        var existing = await context
            .Set<UserRecoveryCodeEntity>()
            .Where(code => code.UserId == userId)
            .ToListAsync(cancellationToken);
        context.Set<UserRecoveryCodeEntity>().RemoveRange(existing);
    }

    /// <summary>Upper case without dashes and whitespace: the form that is hashed.</summary>
    public static string Normalize(string input) =>
        string.Concat(input.Where(character => character != '-' && !char.IsWhiteSpace(character))).ToUpperInvariant();

    private static string NewCode()
    {
        var characters = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            characters[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return $"{new string(characters, 0, CodeLength / 2)}-{new string(characters, CodeLength / 2, CodeLength / 2)}";
    }
}
