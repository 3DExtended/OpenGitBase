namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <param name="Required">False when the session can be issued right away.</param>
/// <param name="ChallengeToken">The opaque token the second step presents; only set when required.</param>
/// <param name="Methods">The second factors the user can answer with, e.g. <c>totp</c>.</param>
public sealed record TwoFactorSignInStart(bool Required, string? ChallengeToken, IReadOnlyList<string> Methods)
{
    public static TwoFactorSignInStart NotRequired { get; } = new(false, null, []);
}
