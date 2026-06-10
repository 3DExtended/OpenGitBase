namespace OpenGitBase.Common.Auth;

public interface IGoogleIdentityTokenValidator
{
    Task ValidateAsync(string identityToken, CancellationToken cancellationToken = default);
}
