using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>Creates a pending authenticator-app secret; it only takes effect once confirmed with a code.</summary>
public class StartTotpSetupQuery : IQuery<TotpSetup, StartTotpSetupQuery>
{
    public UserId UserId { get; set; } = null!;

    /// <summary>The service name the authenticator app shows next to the account.</summary>
    public string Issuer { get; set; } = string.Empty;
}
