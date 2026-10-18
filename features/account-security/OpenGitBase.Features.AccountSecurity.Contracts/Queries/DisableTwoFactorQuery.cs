using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>
/// Turns two-factor sign-in off, removes the authenticator app and the recovery codes, and signs out
/// other sessions.
/// </summary>
public class DisableTwoFactorQuery : IQuery<Unit, DisableTwoFactorQuery>
{
    public UserId UserId { get; set; }
}
