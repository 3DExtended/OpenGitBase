using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Features.AccountSecurity.Challenges;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;

namespace OpenGitBase.Features.AccountSecurity.Passkeys;

/// <summary>
/// The WebAuthn ceremonies around Fido2NetLib. The options handed to the browser are kept in a
/// single-use challenge row, found again by the challenge the browser signed, so any API replica can
/// verify the answer.
/// </summary>
internal static class PasskeyCeremonies
{
    public static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    public static Fido2 CreateRelyingParty(WebAuthnOptions options) =>
        new(
            new Fido2Configuration
            {
                RPID = options.RelyingPartyId,
                RPName = options.RelyingPartyName,
                Origins = new HashSet<string>(options.Origins, StringComparer.Ordinal),
            }
        );

    public static Task StoreChallengeAsync(
        OpenGitBaseDbContext context,
        AuthenticationChallengePurpose purpose,
        Guid userId,
        byte[] challenge,
        string optionsJson,
        DateTimeOffset now,
        CancellationToken cancellationToken
    ) =>
        AuthenticationChallenges.CreateAsync(
            context,
            purpose,
            userId,
            ToBase64Url(challenge),
            ChallengeLifetime,
            now,
            cancellationToken,
            optionsJson
        );

    /// <summary>
    /// Consumes the user's open challenge that <paramref name="clientDataJson"/> answers and returns the
    /// options it was issued with; null when none matches (never issued, already used or expired).
    /// </summary>
    public static async Task<string?> TakeChallengeAsync(
        OpenGitBaseDbContext context,
        AuthenticationChallengePurpose purpose,
        Guid userId,
        byte[] clientDataJson,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var challenge = ReadChallenge(clientDataJson);
        if (string.IsNullOrEmpty(challenge))
        {
            return null;
        }

        var row = await AuthenticationChallenges.FindActiveAsync(context, purpose, challenge, now, cancellationToken);
        if (row is null || row.UserId != userId || row.Payload is null)
        {
            return null;
        }

        return await AuthenticationChallenges.TryConsumeAsync(context, row.Id, cancellationToken) ? row.Payload : null;
    }

    /// <summary>
    /// Verifies the browser's assertion for an open challenge of <paramref name="userId"/>: the signature
    /// must come from one of that user's passkeys, with user verification. A verified assertion records
    /// the new signature counter, backup state and last use.
    /// </summary>
    public static async Task<PasskeyAssertionOutcome> VerifyAssertionAsync(
        OpenGitBaseDbContext context,
        WebAuthnOptions webAuthnOptions,
        AuthenticationChallengePurpose purpose,
        Guid userId,
        string assertionJson,
        DateTimeOffset now,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        var response = Parse<AuthenticatorAssertionRawResponse>(assertionJson);
        if (response?.Response is null)
        {
            return PasskeyAssertionOutcome.Rejected;
        }

        var optionsJson = await TakeChallengeAsync(
            context,
            purpose,
            userId,
            response.Response.ClientDataJson,
            now,
            cancellationToken
        );
        if (optionsJson is null)
        {
            return PasskeyAssertionOutcome.ChallengeExpired;
        }

        var passkey = await context
            .Set<UserPasskeyEntity>()
            .FirstOrDefaultAsync(
                passkey => passkey.CredentialId == response.RawId && passkey.UserId == userId,
                cancellationToken
            );
        if (passkey is null)
        {
            logger.LogInformation("Passkey assertion for user {UserId} rejected: not one of their passkeys", userId);
            return PasskeyAssertionOutcome.Rejected;
        }

        try
        {
            var verified = await CreateRelyingParty(webAuthnOptions)
                .MakeAssertionAsync(
                    new MakeAssertionParams
                    {
                        AssertionResponse = response,
                        OriginalOptions = AssertionOptions.FromJson(optionsJson),
                        StoredPublicKey = passkey.PublicKey,
                        StoredSignatureCounter = (uint)passkey.SignCount,
                        IsUserHandleOwnerOfCredentialIdCallback = (candidate, _) =>
                            Task.FromResult(candidate.UserHandle is { Length: 16 } && new Guid(candidate.UserHandle) == userId),
                    },
                    cancellationToken
                );
            passkey.SignCount = verified.SignCount;
            passkey.IsBackedUp = verified.IsBackedUp;
            passkey.LastUsedAt = now;
            await context.SaveChangesAsync(cancellationToken);
            return PasskeyAssertionOutcome.Verified;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogInformation(
                "Passkey assertion for user {UserId} rejected: {Reason}",
                userId,
                exception.Message
            );
            return PasskeyAssertionOutcome.Rejected;
        }
    }

    /// <summary>The user's passkeys as credential descriptors, for <c>excludeCredentials</c> or <c>allowCredentials</c>.</summary>
    public static async Task<List<PublicKeyCredentialDescriptor>> DescriptorsAsync(
        OpenGitBaseDbContext context,
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        var passkeys = await context
            .Set<UserPasskeyEntity>()
            .AsNoTracking()
            .Where(passkey => passkey.UserId == userId)
            .Select(passkey => new { passkey.CredentialId, passkey.Transports })
            .ToListAsync(cancellationToken);
        return passkeys
            .Select(passkey => new PublicKeyCredentialDescriptor(
                PublicKeyCredentialType.PublicKey,
                passkey.CredentialId,
                ParseTransports(passkey.Transports)
            ))
            .ToList();
    }

    /// <summary>Parses untrusted JSON from the browser; null when it is not a credential at all.</summary>
    public static T? Parse<T>(string json)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static PasskeySummary Summarize(UserPasskeyEntity passkey) =>
        new(passkey.Id, passkey.Name, passkey.CreatedAt, passkey.LastUsedAt, passkey.IsBackedUp);

    public static string FormatTransports(AuthenticatorTransport[]? transports) =>
        transports is null ? string.Empty : string.Join(",", transports.Select(transport => transport.ToString()));

    private static AuthenticatorTransport[] ParseTransports(string transports) =>
        transports
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => Enum.TryParse<AuthenticatorTransport>(name, out var transport) ? transport : (AuthenticatorTransport?)null)
            .OfType<AuthenticatorTransport>()
            .ToArray();

    private static string? ReadChallenge(byte[] clientDataJson)
    {
        try
        {
            using var document = JsonDocument.Parse(clientDataJson);
            return document.RootElement.TryGetProperty("challenge", out var challenge)
                && challenge.ValueKind == JsonValueKind.String
                ? challenge.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
