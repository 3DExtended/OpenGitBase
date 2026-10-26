using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <param name="UserId">The signed-in user; only set on success.</param>
/// <param name="LockedUntil">When the second step opens again; only set when locked out.</param>
public sealed record TwoFactorSignInResult(
    TwoFactorSignInStatus Status,
    UserId? UserId = null,
    DateTimeOffset? LockedUntil = null
);
