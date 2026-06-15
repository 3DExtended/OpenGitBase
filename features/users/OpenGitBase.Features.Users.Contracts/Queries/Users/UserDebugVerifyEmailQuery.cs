using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Users.Contracts.Queries.Users;

public class UserDebugVerifyEmailQuery : IQuery<Unit, UserDebugVerifyEmailQuery>
{
    public UserId UserId { get; set; } = null!;
}
