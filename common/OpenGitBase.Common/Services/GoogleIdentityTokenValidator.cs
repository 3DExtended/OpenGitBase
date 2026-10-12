using Google.Apis.Auth;

using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Options;

namespace OpenGitBase.Common.Services;

public class GoogleIdentityTokenValidator : IGoogleIdentityTokenValidator
{
    private readonly GoogleAuthOptions _options;

    public GoogleIdentityTokenValidator(GoogleAuthOptions options)
    {
        _options = options;
    }

    public Task ValidateAsync(string identityToken, CancellationToken cancellationToken = default)
    {
        if (_options.TrustedClientIds().Count == 0)
        {
            // Without an audience Google would accept ID tokens minted for any OAuth client.
            throw new InvalidJwtException("Google sign-in is not configured.");
        }

        return GoogleJsonWebSignature.ValidateAsync(identityToken, CreateValidationSettings());
    }

    public GoogleJsonWebSignature.ValidationSettings CreateValidationSettings() =>
        new() { Audience = _options.TrustedClientIds() };
}
