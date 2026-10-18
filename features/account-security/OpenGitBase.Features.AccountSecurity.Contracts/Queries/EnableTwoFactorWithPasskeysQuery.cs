using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>
/// Turns 2FA on with the user's passkeys as the second factor, without an authenticator app. Like the
/// first authenticator confirmation, it returns a fresh set of recovery codes once and revokes every
/// session issued before.
/// </summary>
public class EnableTwoFactorWithPasskeysQuery : IQuery<PasskeyTwoFactorActivation, EnableTwoFactorWithPasskeysQuery>
{
    public UserId UserId { get; set; } = null!;
}
