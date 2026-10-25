using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>
/// Creation options for a new passkey: discoverable, user-verified, no attestation, excluding the
/// user's existing passkeys. The challenge is single-use and expires after 5 minutes.
/// </summary>
public class StartPasskeyRegistrationQuery : IQuery<PasskeyCeremonyOptions, StartPasskeyRegistrationQuery>
{
    public UserId UserId { get; set; } = null!;
}
