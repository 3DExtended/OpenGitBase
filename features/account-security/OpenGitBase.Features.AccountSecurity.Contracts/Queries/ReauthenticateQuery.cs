using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>
/// Confirms the signed-in user again before a security-sensitive change: the password, plus a code
/// or a recovery code when 2FA is on. Success is remembered for <see cref="SessionId"/> only.
/// </summary>
public class ReauthenticateQuery : IQuery<ReauthenticationResult, ReauthenticateQuery>
{
    public UserId UserId { get; set; }

    /// <summary>The session token's <c>jti</c>.</summary>
    public string SessionId { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? Code { get; set; }

    /// <summary>Used instead of <see cref="Code"/> when set, so a lost phone can still be replaced.</summary>
    public string? RecoveryCode { get; set; }
}
