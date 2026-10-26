using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>
/// Request options for signing in with any registered passkey. No credential is listed, so the browser
/// offers the discoverable passkeys it holds and nothing reveals which accounts exist. The challenge
/// belongs to nobody, is single-use and expires after 5 minutes.
/// </summary>
public class StartPasskeySignInQuery : IQuery<PasskeyCeremonyOptions, StartPasskeySignInQuery>
{
}
