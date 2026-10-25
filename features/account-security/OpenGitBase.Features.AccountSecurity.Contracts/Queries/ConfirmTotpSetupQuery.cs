using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>
/// Activates the pending authenticator-app secret when the code matches it. The first confirmation
/// turns two-factor sign-in on and signs out every other session.
/// </summary>
public class ConfirmTotpSetupQuery : IQuery<TotpConfirmation, ConfirmTotpSetupQuery>
{
    public UserId UserId { get; set; } = null!;

    public string Code { get; set; } = string.Empty;
}
