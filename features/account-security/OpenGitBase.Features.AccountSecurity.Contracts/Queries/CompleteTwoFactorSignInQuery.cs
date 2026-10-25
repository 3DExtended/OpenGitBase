using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>
/// Answers a two-factor sign-in challenge with a code from the authenticator app, with one of the
/// user's recovery codes when <see cref="RecoveryCode"/> is set, or with a passkey when
/// <see cref="PasskeyAssertionJson"/> is set.
/// </summary>
public class CompleteTwoFactorSignInQuery : IQuery<TwoFactorSignInResult, CompleteTwoFactorSignInQuery>
{
    public string ChallengeToken { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? RecoveryCode { get; set; }

    /// <summary>An assertion for <see cref="StartTwoFactorPasskeyQuery"/>, as <c>PublicKeyCredential.toJSON()</c> returns it.</summary>
    public string? PasskeyAssertionJson { get; set; }
}
