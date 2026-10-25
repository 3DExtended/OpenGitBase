using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Issues and checks the tokens the batch response hands to the LFS client for each transfer. A token is
/// <c>{payload}.{signature}</c>: base64url JSON naming repository, oid, size, operation and expiry, signed with
/// HMAC-SHA256 under a key derived from the JWT signing key. It is not a JWT, so the API's bearer
/// authentication never accepts it, and a JWT never passes as an action token. The dispatcher has it checked
/// when a transfer starts; a transfer that started in time may run past the expiry.
/// </summary>
public sealed class LfsActionTokenService
{
    private static readonly byte[] KeyDerivationLabel = Encoding.UTF8.GetBytes("opengitbase:lfs-action-token:v1");
    private readonly byte[] _signingKey;
    private readonly LfsOptions _lfsOptions;
    private readonly ISystemClock _clock;

    public LfsActionTokenService(JwtOptions jwtOptions, LfsOptions lfsOptions, ISystemClock clock)
    {
        // Same fallback as the JWT bearer setup in Startup, so development without a key still works.
        var jwtKey = string.IsNullOrEmpty(jwtOptions.Key)
            ? string.Join(string.Empty, Enumerable.Repeat("dev-key", 32))
            : jwtOptions.Key;
        _signingKey = HMACSHA256.HashData(Encoding.UTF8.GetBytes(jwtKey), KeyDerivationLabel);
        _lfsOptions = lfsOptions;
        _clock = clock;
    }

    public LfsIssuedActionToken Issue(
        Guid repositoryId,
        string oid,
        long size,
        LfsTransferOperation operation,
        Guid? userId
    )
    {
        var lifetime = Math.Max(1, _lfsOptions.ActionTokenLifetimeSeconds);
        var expiresAt = _clock.UtcNow.AddSeconds(lifetime);
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new TokenPayload
            {
                RepositoryId = repositoryId,
                Oid = oid,
                Size = size,
                Operation = operation,
                UserId = userId,
                ExpiresAtUnixSeconds = expiresAt.ToUnixTimeSeconds(),
            }
        );
        var encodedPayload = WebEncoders.Base64UrlEncode(payload);
        var token = $"{encodedPayload}.{WebEncoders.Base64UrlEncode(Sign(encodedPayload))}";
        return new LfsIssuedActionToken(token, DateTimeOffset.FromUnixTimeSeconds(expiresAt.ToUnixTimeSeconds()), lifetime);
    }

    public LfsActionTokenValidation Validate(string token, string oid, LfsTransferOperation operation)
    {
        var claims = TryReadSignedClaims(token);
        if (claims is null)
        {
            return LfsActionTokenValidation.Invalid("The LFS action token is invalid.");
        }

        if (!string.Equals(claims.Oid, oid, StringComparison.Ordinal) || claims.Operation != operation)
        {
            return LfsActionTokenValidation.Invalid("The LFS action token does not cover this transfer.");
        }

        if (_clock.UtcNow >= claims.ExpiresAt)
        {
            return LfsActionTokenValidation.Invalid("The LFS action token has expired; run the command again.");
        }

        return LfsActionTokenValidation.Valid(claims);
    }

    private LfsActionTokenClaims? TryReadSignedClaims(string token)
    {
        var parts = (token ?? string.Empty).Split('.');
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            return null;
        }

        try
        {
            var signature = WebEncoders.Base64UrlDecode(parts[1]);
            if (!CryptographicOperations.FixedTimeEquals(signature, Sign(parts[0])))
            {
                return null;
            }

            var payload = JsonSerializer.Deserialize<TokenPayload>(WebEncoders.Base64UrlDecode(parts[0]));
            if (payload is null || payload.Oid is null)
            {
                return null;
            }

            return new LfsActionTokenClaims(
                payload.RepositoryId,
                payload.Oid,
                payload.Size,
                payload.Operation,
                payload.UserId,
                DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAtUnixSeconds)
            );
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }

    private byte[] Sign(string encodedPayload) =>
        HMACSHA256.HashData(_signingKey, Encoding.ASCII.GetBytes(encodedPayload));

    private sealed class TokenPayload
    {
        [System.Text.Json.Serialization.JsonPropertyName("r")]
        public Guid RepositoryId { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("o")]
        public string? Oid { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("s")]
        public long Size { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("op")]
        public LfsTransferOperation Operation { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("u")]
        public Guid? UserId { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("exp")]
        public long ExpiresAtUnixSeconds { get; init; }
    }
}
