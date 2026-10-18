using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>
/// Confirms the signed-in user again with a passkey assertion instead of the password. A user-verified
/// passkey is two factors in one, so no code is asked for. Success is remembered for
/// <see cref="SessionId"/> only.
/// </summary>
public class ReauthenticateWithPasskeyQuery : IQuery<ReauthenticationResult, ReauthenticateWithPasskeyQuery>
{
    public UserId UserId { get; set; } = null!;

    /// <summary>The session token's <c>jti</c>.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>The assertion as <c>PublicKeyCredential.toJSON()</c> returns it.</summary>
    public string AssertionResponseJson { get; set; } = string.Empty;
}
