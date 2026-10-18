using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>
/// Answers a two-factor sign-in challenge with a code from the authenticator app or, when
/// <see cref="RecoveryCode"/> is set, with one of the user's recovery codes.
/// </summary>
public class CompleteTwoFactorSignInQuery : IQuery<TwoFactorSignInResult, CompleteTwoFactorSignInQuery>
{
    public string ChallengeToken { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? RecoveryCode { get; set; }
}
