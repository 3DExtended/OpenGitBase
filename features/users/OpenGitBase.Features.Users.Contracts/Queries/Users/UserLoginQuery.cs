using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Users.Contracts.Queries.Users;

public class UserLoginQuery : IQuery<UserId, UserLoginQuery>
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
