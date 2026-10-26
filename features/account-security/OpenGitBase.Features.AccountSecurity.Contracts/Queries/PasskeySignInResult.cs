using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <param name="UserId">The signed-in user; only set on success.</param>
public sealed record PasskeySignInResult(PasskeySignInStatus Status, UserId? UserId = null);
