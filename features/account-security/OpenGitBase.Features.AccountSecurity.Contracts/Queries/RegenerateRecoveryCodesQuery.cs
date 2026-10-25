using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>
/// Replaces every recovery code of the user, used or not, with a new set. Returns none when the user is
/// unknown or two-factor sign-in is off.
/// </summary>
public class RegenerateRecoveryCodesQuery : IQuery<RecoveryCodeSet, RegenerateRecoveryCodesQuery>
{
    public UserId UserId { get; set; } = null!;
}
