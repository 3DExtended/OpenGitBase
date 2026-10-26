using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>The user's own passkeys, oldest first.</summary>
public class ListPasskeysQuery : IQuery<PasskeyList, ListPasskeysQuery>
{
    public UserId UserId { get; set; } = null!;
}
