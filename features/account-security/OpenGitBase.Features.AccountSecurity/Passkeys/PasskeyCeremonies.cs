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
using OpenGitBase.Features.AccountSecurity.Events;

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
        Guid? userId,
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
    /// options it was issued with; null when none matches (never issued, already used or expired). A
    /// null <paramref name="userId"/> takes an anonymous challenge, as issued for a passkey sign-in.
    /// </summary>
    public static async Task<string?> TakeChallengeAsync(
        OpenGitBaseDbContext context,
        AuthenticationChallengePurpose purpose,
        Guid? userId,
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
    /// must come from one of that user's passkeys, with user verification. Without a user (sign-in), any
    /// registered passkey may answer an anonymous challenge, and the result names its owner. A verified
    /// assertion records the new signature counter, backup state and last use. A counter that does not
    /// grow is rejected unless it stays 0, as synced passkeys report. Without
    /// <paramref name="requireUserVerification"/> (a second factor after the password), the
    /// authenticator only has to prove presence.
    /// </summary>
    public static async Task<PasskeyAssertion> VerifyAssertionAsync(
        OpenGitBaseDbContext context,
        WebAuthnOptions webAuthnOptions,
        AuthenticationChallengePurpose purpose,
        Guid? userId,
        string assertionJson,
        DateTimeOffset now,
        ILogger logger,
        string? remoteIp,
        CancellationToken cancellationToken,
        bool requireUserVerification = true
    )
    {
        var response = Parse<AuthenticatorAssertionRawResponse>(assertionJson);
        if (response?.Response is null)
        {
            return PasskeyAssertion.Rejected;
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
            return new PasskeyAssertion(PasskeyAssertionOutcome.ChallengeExpired);
        }

        var candidates = context.Set<UserPasskeyEntity>().Where(passkey => passkey.CredentialId == response.RawId);
        if (userId is not null)
        {
            candidates = candidates.Where(passkey => passkey.UserId == userId);
        }

        var passkey = await candidates.FirstOrDefaultAsync(cancellationToken);
        if (passkey is null)
        {
            SecurityEventLog.Write(
                logger,
                SecurityEvent.PasskeyRejected,
                userId,
                remoteIp,
                userId is null ? "reason=unknown_credential" : "reason=not_their_passkey"
            );

            return PasskeyAssertion.Rejected;
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
                            Task.FromResult(
                                candidate.UserHandle is { Length: 16 } && new Guid(candidate.UserHandle) == passkey.UserId
                            ),
                    },
                    cancellationToken
                );
            if (requireUserVerification && !IsUserVerified(response.Response.AuthenticatorData))
            {
                // The options may only prefer verification; on its own, a passkey must have verified the user.
                SecurityEventLog.Write(logger, SecurityEvent.PasskeyRejected, passkey.UserId, remoteIp, "reason=user_not_verified");
                return PasskeyAssertion.Rejected;
            }

            passkey.SignCount = verified.SignCount;
            passkey.IsBackedUp = verified.IsBackedUp;
            passkey.LastUsedAt = now;
            await context.SaveChangesAsync(cancellationToken);
            return new PasskeyAssertion(PasskeyAssertionOutcome.Verified, passkey.UserId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            SecurityEventLog.Write(
                logger,
                SecurityEvent.PasskeyRejected,
                passkey.UserId,
                remoteIp,
                $"reason={exception.Message}"
            );
            return PasskeyAssertion.Rejected;
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

    /// <summary>The UV flag (bit 2 of the flags byte after the RP ID hash); the signature covers it.</summary>
    private static bool IsUserVerified(byte[] authenticatorData) =>
        authenticatorData.Length > 32 && (authenticatorData[32] & 0x04) != 0;

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
