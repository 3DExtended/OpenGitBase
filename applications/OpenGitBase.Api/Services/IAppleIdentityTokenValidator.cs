namespace OpenGitBase.Api.Services;

public interface IAppleIdentityTokenValidator
{
    /// <summary>
    /// Verifies an Apple ID token (signature, issuer, audience, lifetime) and returns the
    /// verified identity, or <c>null</c> when the token must not be trusted.
    /// </summary>
    Task<AppleIdentity?> ValidateAsync(
        string identityToken,
        CancellationToken cancellationToken = default
    );
}
