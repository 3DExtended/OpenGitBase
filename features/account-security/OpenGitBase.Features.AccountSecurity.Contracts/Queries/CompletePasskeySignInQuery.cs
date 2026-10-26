using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>
/// Signs in the owner of the passkey that answered an open sign-in challenge. A user-verified passkey
/// is two factors in one, so there is no second step even with 2FA on. Every failure looks the same.
/// </summary>
public class CompletePasskeySignInQuery : IQuery<PasskeySignInResult, CompletePasskeySignInQuery>
{
    /// <summary>The assertion as <c>PublicKeyCredential.toJSON()</c> returns it.</summary>
    public string AssertionResponseJson { get; set; } = string.Empty;
}
