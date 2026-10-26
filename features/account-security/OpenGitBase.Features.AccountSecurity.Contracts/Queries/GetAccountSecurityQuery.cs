using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

public class GetAccountSecurityQuery : IQuery<AccountSecurityStatus, GetAccountSecurityQuery>
{
    public UserId UserId { get; set; }
}
