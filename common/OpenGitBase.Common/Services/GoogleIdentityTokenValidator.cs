using Google.Apis.Auth;

using OpenGitBase.Common.Auth;

namespace OpenGitBase.Common.Services;

public class GoogleIdentityTokenValidator : IGoogleIdentityTokenValidator
{
    public Task ValidateAsync(string identityToken, CancellationToken cancellationToken = default) =>
        GoogleJsonWebSignature.ValidateAsync(identityToken);
}
