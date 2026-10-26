using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>
/// Request options for answering an open two-factor sign-in with one of the user's passkeys. After
/// the password, user verification is only preferred: a security-key touch is enough.
/// </summary>
public class StartTwoFactorPasskeyQuery : IQuery<TwoFactorPasskeyOptions, StartTwoFactorPasskeyQuery>
{
    /// <summary>The token from the first sign-in step.</summary>
    public string ChallengeToken { get; set; } = string.Empty;
}
