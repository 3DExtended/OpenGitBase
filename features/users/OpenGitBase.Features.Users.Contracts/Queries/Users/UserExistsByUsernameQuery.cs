using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Users.Contracts.Queries.Users;

public class UserExistsByUsernameQuery : IQuery<bool, UserExistsByUsernameQuery>
{
    public string Username { get; set; } = string.Empty;
}
