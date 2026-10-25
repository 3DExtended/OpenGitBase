using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>
/// Runs after a successful first sign-in step (password, Google or Apple). With two-factor sign-in
/// on, it issues the challenge the second step must answer instead of a session.
/// </summary>
public class BeginTwoFactorSignInQuery : IQuery<TwoFactorSignInStart, BeginTwoFactorSignInQuery>
{
    public UserId UserId { get; set; } = null!;
}
