using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Users.Contracts.Queries.Users;

public class UserExistsByUsernameQuery : IQuery<UserId, UserExistsByUsernameQuery>
{
    public string Username { get; set; } = string.Empty;
}
