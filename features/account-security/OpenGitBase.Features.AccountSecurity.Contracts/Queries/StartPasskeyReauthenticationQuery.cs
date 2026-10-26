using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>
/// Request options for confirming the user again with one of their passkeys; none when they have no
/// passkey. The challenge is single-use and expires after 5 minutes.
/// </summary>
public class StartPasskeyReauthenticationQuery : IQuery<PasskeyCeremonyOptions, StartPasskeyReauthenticationQuery>
{
    public UserId UserId { get; set; } = null!;
}
