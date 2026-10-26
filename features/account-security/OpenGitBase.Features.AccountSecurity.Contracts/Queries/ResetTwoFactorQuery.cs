using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>
/// An administrator's way out for a user who lost every second factor: removes the authenticator app,
/// the recovery codes and all passkeys, turns 2FA off, clears the lockout, revokes every session and
/// emails the user. None when no live account has the username.
/// </summary>
public class ResetTwoFactorQuery : IQuery<Unit, ResetTwoFactorQuery>
{
    public string Username { get; set; } = string.Empty;

    public UserId AdminUserId { get; set; } = null!;
}
