using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Users.Contracts.Queries.Users;

public class UserGetByInternalIdQuery : IQuery<UserId, UserGetByInternalIdQuery>
{
    public string InternalId { get; set; } = string.Empty;
}
