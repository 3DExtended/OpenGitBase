using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>
/// Confirms the signed-in user again with a passkey assertion. A user-verified passkey is two factors
/// in one, so neither password nor code is asked for. Together with the correct <see cref="Password"/>,
/// the passkey stands in for the authenticator code and user verification is not needed. Success is
/// remembered for <see cref="SessionId"/> only.
/// </summary>
public class ReauthenticateWithPasskeyQuery : IQuery<ReauthenticationResult, ReauthenticateWithPasskeyQuery>
{
    public UserId UserId { get; set; } = null!;

    /// <summary>The session token's <c>jti</c>.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>The assertion as <c>PublicKeyCredential.toJSON()</c> returns it.</summary>
    public string AssertionResponseJson { get; set; } = string.Empty;

    /// <summary>Empty when the user confirms with the passkey alone.</summary>
    public string Password { get; set; } = string.Empty;
}
